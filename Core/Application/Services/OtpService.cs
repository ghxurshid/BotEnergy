using System.Collections.Concurrent;
using System.Security.Cryptography;
using Domain.Auth;
using Domain.Enums;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
    /// <summary>
    /// In-memory OTP (single-instance AuthApi uchun; restart'da reset bo'ladi).
    /// TTL, urinishlar limiti va thread-safe saqlash bilan.
    /// Test kodi "123456" faqat <see cref="OtpSettings.AllowTestCode"/> true bo'lsa o'tadi
    /// (Development'da yoqiladi, Production'da o'chiq).
    /// </summary>
    public class OtpService : IOtpService
    {
        private sealed record OtpEntry(string Code, DateTime ExpiresAt, int Attempts);

        /// <summary>Tasdiqlangan holat qancha turadi (parol o'rnatishga ulgurish uchun OTP TTL'dan uzunroq).</summary>
        private const int VerifiedWindowMinutes = 10;

        private readonly ConcurrentDictionary<string, OtpEntry> _otpStorage = new();
        private readonly ConcurrentDictionary<string, DateTime> _verifiedStorage = new();

        private readonly OtpSettings _settings;
        private readonly ILogger<OtpService> _logger;

        public OtpService(OtpSettings settings, ILogger<OtpService> logger)
        {
            _settings = settings;
            _logger = logger;
        }

        private static string Key(string phoneNumber, OtpPurpose purpose)
            => $"{phoneNumber}:{purpose}";

        public Task<string> GenerateOtpAsync(string phoneNumber, OtpPurpose purpose)
        {
            PruneExpired();

            var code = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
            var key = Key(phoneNumber, purpose);

            _otpStorage[key] = new OtpEntry(code, DateTime.Now.AddMinutes(_settings.TtlMinutes), 0);
            _verifiedStorage.TryRemove(key, out _);

            // SMS provider ulangunga qadar kod log'da ko'rinadi (faqat dev oqimi uchun).
            _logger.LogInformation("OTP generated for {Phone} [{Purpose}]: {Code}", phoneNumber, purpose, code);

            return Task.FromResult(code);
        }

        public Task<bool> VerifyOtpAsync(string phoneNumber, string code, OtpPurpose purpose)
        {
            PruneExpired();

            var key = Key(phoneNumber, purpose);

            if (_settings.AllowTestCode && code == "123456")
            {
                _verifiedStorage[key] = DateTime.Now.AddMinutes(VerifiedWindowMinutes);
                return Task.FromResult(true);
            }

            if (!_otpStorage.TryGetValue(key, out var entry))
                return Task.FromResult(false);

            if (entry.ExpiresAt < DateTime.Now)
            {
                _otpStorage.TryRemove(key, out _);
                return Task.FromResult(false);
            }

            if (entry.Attempts >= _settings.MaxAttempts)
            {
                // Limit tugadi — kod bekor qilinadi, foydalanuvchi yangi OTP so'rashi kerak.
                _otpStorage.TryRemove(key, out _);
                _logger.LogWarning("OTP attempt limit exceeded for {Phone} [{Purpose}]", phoneNumber, purpose);
                return Task.FromResult(false);
            }

            var isMatch = CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(entry.Code),
                System.Text.Encoding.UTF8.GetBytes(code));

            if (!isMatch)
            {
                _otpStorage.TryUpdate(key, entry with { Attempts = entry.Attempts + 1 }, entry);
                return Task.FromResult(false);
            }

            _otpStorage.TryRemove(key, out _);
            _verifiedStorage[key] = DateTime.Now.AddMinutes(VerifiedWindowMinutes);
            return Task.FromResult(true);
        }

        public Task<bool> IsOtpVerifiedAsync(string phoneNumber, OtpPurpose purpose)
        {
            var key = Key(phoneNumber, purpose);
            if (_verifiedStorage.TryGetValue(key, out var expiresAt) && expiresAt >= DateTime.Now)
                return Task.FromResult(true);

            _verifiedStorage.TryRemove(key, out _);
            return Task.FromResult(false);
        }

        public Task ConsumeOtpVerificationAsync(string phoneNumber, OtpPurpose purpose)
        {
            _verifiedStorage.TryRemove(Key(phoneNumber, purpose), out _);
            return Task.CompletedTask;
        }

        private void PruneExpired()
        {
            var now = DateTime.Now;

            foreach (var pair in _otpStorage)
                if (pair.Value.ExpiresAt < now)
                    _otpStorage.TryRemove(pair.Key, out _);

            foreach (var pair in _verifiedStorage)
                if (pair.Value < now)
                    _verifiedStorage.TryRemove(pair.Key, out _);
        }
    }
}
