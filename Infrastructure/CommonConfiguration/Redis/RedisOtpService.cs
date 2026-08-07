using System.Security.Cryptography;
using System.Text;
using Domain.Auth;
using Domain.Enums;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CommonConfiguration.Redis
{
    /// <summary>
    /// OTP holatini Redis'da saqlaydi — AuthApi'ni bir nechta replikaga chiqarish uchun shart.
    ///
    /// In-memory variant (<c>Application.Services.OtpService</c>) bilan muammo: 1-instansiyada
    /// yaratilgan kod 2-instansiyada topilmaydi. Load balancer so'rovlarni almashtirib
    /// yuborgani uchun login tasodifiy ravishda ishlamay qoladi va sabab loglardan ko'rinmaydi.
    ///
    /// TTL Redis'ning o'zida — <c>PruneExpired</c> kabi qo'lda tozalash kerak emas.
    /// Urinishlar soni <c>HINCRBY</c> bilan atomik oshiriladi: bir vaqtda kelgan ikki
    /// urinish limitni chetlab o'ta olmaydi.
    /// </summary>
    public sealed class RedisOtpService : IOtpService
    {
        private const string OtpKeyPrefix = "otp:code:";
        private const string VerifiedKeyPrefix = "otp:verified:";
        private const string CodeField = "code";
        private const string AttemptsField = "attempts";

        /// <summary>Tasdiqlangan holat qancha turadi (parol o'rnatishga ulgurish uchun OTP TTL'dan uzunroq).</summary>
        private static readonly TimeSpan VerifiedWindow = TimeSpan.FromMinutes(10);

        private readonly IConnectionMultiplexer _redis;
        private readonly OtpSettings _settings;
        private readonly ILogger<RedisOtpService> _logger;

        public RedisOtpService(
            IConnectionMultiplexer redis,
            OtpSettings settings,
            ILogger<RedisOtpService> logger)
        {
            _redis = redis;
            _settings = settings;
            _logger = logger;
        }

        private static string OtpKey(string phoneNumber, OtpPurpose purpose)
            => $"{OtpKeyPrefix}{phoneNumber}:{purpose}";

        private static string VerifiedKey(string phoneNumber, OtpPurpose purpose)
            => $"{VerifiedKeyPrefix}{phoneNumber}:{purpose}";

        public async Task<string> GenerateOtpAsync(string phoneNumber, OtpPurpose purpose)
        {
            var code = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
            var db = _redis.GetDatabase();
            var key = OtpKey(phoneNumber, purpose);

            // Yangi kod eski urinishlar hisobini ham nolga tushiradi.
            await db.KeyDeleteAsync(key);
            await db.HashSetAsync(key, new[]
            {
                new HashEntry(CodeField, code),
                new HashEntry(AttemptsField, 0)
            });
            await db.KeyExpireAsync(key, TimeSpan.FromMinutes(_settings.TtlMinutes));
            await db.KeyDeleteAsync(VerifiedKey(phoneNumber, purpose));

            // SMS provider ulangunga qadar kod log'da ko'rinadi (faqat dev oqimi uchun).
            _logger.LogInformation("OTP generated for {Phone} [{Purpose}]: {Code}", phoneNumber, purpose, code);

            return code;
        }

        public async Task<bool> VerifyOtpAsync(string phoneNumber, string code, OtpPurpose purpose)
        {
            var db = _redis.GetDatabase();

            if (_settings.AllowTestCode && code == "123456")
            {
                await MarkVerifiedAsync(db, phoneNumber, purpose);
                return true;
            }

            var key = OtpKey(phoneNumber, purpose);
            var stored = await db.HashGetAsync(key, CodeField);
            if (stored.IsNullOrEmpty)
                return false;   // kod yo'q yoki TTL tugagan

            // Urinishni OLDIN hisoblaymiz — noto'g'ri kod bilan cheksiz urinishning oldini oladi.
            var attempts = await db.HashIncrementAsync(key, AttemptsField);
            if (attempts > _settings.MaxAttempts)
            {
                await db.KeyDeleteAsync(key);
                _logger.LogWarning("OTP attempt limit exceeded for {Phone} [{Purpose}]", phoneNumber, purpose);
                return false;
            }

            var isMatch = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(stored!),
                Encoding.UTF8.GetBytes(code));

            if (!isMatch)
                return false;

            await db.KeyDeleteAsync(key);
            await MarkVerifiedAsync(db, phoneNumber, purpose);
            return true;
        }

        public async Task<bool> IsOtpVerifiedAsync(string phoneNumber, OtpPurpose purpose)
            => await _redis.GetDatabase().KeyExistsAsync(VerifiedKey(phoneNumber, purpose));

        public Task ConsumeOtpVerificationAsync(string phoneNumber, OtpPurpose purpose)
            => _redis.GetDatabase().KeyDeleteAsync(VerifiedKey(phoneNumber, purpose));

        private static Task MarkVerifiedAsync(IDatabase db, string phoneNumber, OtpPurpose purpose)
            => db.StringSetAsync(VerifiedKey(phoneNumber, purpose), "1", VerifiedWindow);
    }
}
