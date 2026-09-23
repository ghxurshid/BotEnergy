using System.Security.Cryptography;
using Domain.Dtos.Base;
using Domain.Dtos.Telegram;
using Domain.Enums;
using Domain.Guards;
using Domain.Helpers;
using Domain.Interfaces.Telegram;
using Domain.Options;
using Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Services
{
    /// <summary>
    /// Tasdiqlash kodlarini Telegram orqali yetkazish.
    ///
    /// **Bog'lash xavfsizligi.** Havola ichidagi token qaysi profil uchun ochilganini
    /// aytadi, lekin chat faqat Telegram O'ZI tasdiqlagan telefon raqami profil
    /// raqamiga to'g'ri kelgandagina bog'lanadi. Shu sabab birovning userId'si bilan
    /// havola yasab, uning kodlarini o'ziga burib yuborib bo'lmaydi.
    /// </summary>
    public sealed class TelegramGateway : ITelegramGateway
    {
        private readonly ITelegramBotClient _bot;
        private readonly ITelegramLinkStore _links;
        private readonly ICustomerUserRepository _users;
        private readonly TelegramOptions _options;
        private readonly ILogger<TelegramGateway> _logger;

        public TelegramGateway(
            ITelegramBotClient bot,
            ITelegramLinkStore links,
            ICustomerUserRepository users,
            IOptions<TelegramOptions> options,
            ILogger<TelegramGateway> logger)
        {
            _bot = bot;
            _links = links;
            _users = users;
            _options = options.Value;
            _logger = logger;
        }

        // ── 1. Ilova uchun havola ────────────────────────────────────────────

        public async Task<GenericDto<TelegramLinkDto>> CreateLinkAsync(long userId)
        {
            var user = await _users.GetByIdAsync(userId);

            var stop = StopFactorCheck.For("Telegram.CreateLink")
                .StopIf(user is null, StopFactors.User.NotFound)
                .StopIf(() => string.IsNullOrWhiteSpace(_options.BotUsername), StopFactors.Telegram.NotConfigured)
                .Result();

            if (stop is not null)
                return GenericDto<TelegramLinkDto>.Blocked(stop);

            var ttl = TimeSpan.FromMinutes(_options.LinkTtlMinutes > 0 ? _options.LinkTtlMinutes : 15);
            var token = GenerateToken();

            await _links.SetAsync(token, userId, ttl);

            return GenericDto<TelegramLinkDto>.Success(new TelegramLinkDto
            {
                Url = $"https://t.me/{_options.BotUsername.TrimStart('@')}?start={token}",
                BotUsername = _options.BotUsername.TrimStart('@'),
                ExpiresAt = DateTime.Now.Add(ttl),
                IsLinked = user!.TelegramChatId is not null
            });
        }

        // ── 2. Botdan kelgan xabarlar ────────────────────────────────────────

        public async Task HandleUpdateAsync(TelegramUpdate update, CancellationToken ct = default)
        {
            if (update.ChatId is null) return;
            var chatId = update.ChatId.Value;

            // 2a. Telefon raqam ulashildi — bog'lash shu yerda yakunlanadi.
            if (!string.IsNullOrWhiteSpace(update.ContactPhone))
            {
                await BindByContactAsync(chatId, update, ct);
                return;
            }

            var text = (update.Text ?? string.Empty).Trim();

            if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
            {
                var payload = text.Length > 6 ? text[6..].Trim() : string.Empty;

                // Havoladagi token qaysi profil ekanini aytadi — uni eslab qo'yamiz.
                if (!string.IsNullOrEmpty(payload))
                {
                    var userId = await _links.ConsumeAsync(payload);
                    if (userId is not null)
                        _pendingProfiles[chatId] = userId.Value;
                }

                await _bot.RequestContactAsync(
                    chatId,
                    "<b>BotEnergy</b>\n\nTasdiqlash kodlarini shu yerda olish uchun telefon raqamingizni ulashing. " +
                    "Raqam ilovadagi hisobingiz raqami bilan bir xil bo'lishi kerak.",
                    "📱 Telefon raqamni ulashish",
                    ct);
                return;
            }

            if (text.StartsWith("/help", StringComparison.OrdinalIgnoreCase))
            {
                await _bot.SendMessageAsync(chatId,
                    "Bu bot BotEnergy ilovasi uchun tasdiqlash kodlarini yuboradi.\n\n" +
                    "Hisobni bog'lash: ilovada \"Kodni Telegram orqali olish\" tugmasini bosing, " +
                    "so'ng bu yerda telefon raqamingizni ulashing.", ct);
                return;
            }

            await _bot.SendMessageAsync(chatId,
                "Tasdiqlash kodlarini olish uchun telefon raqamingizni ulashing — /start buyrug'ini bosing.", ct);
        }

        /// <summary>
        /// Havola orqali kelgan profil "kutish" ro'yxati: chat → userId.
        /// Faqat bitta jarayon ichida yashaydi (AuthApi bitta instans), yo'qolsa ham
        /// bog'lash telefon raqam bo'yicha baribir ishlaydi.
        /// </summary>
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<long, long> _pendingProfiles = new();

        private async Task BindByContactAsync(long chatId, TelegramUpdate update, CancellationToken ct)
        {
            // Boshqa odamning kontaktini yuborish orqali bog'lanib bo'lmaydi.
            if (update.ContactUserId is not null && update.FromUserId is not null &&
                update.ContactUserId != update.FromUserId)
            {
                await _bot.SendMessageAsync(chatId,
                    "❌ Faqat <b>o'zingizning</b> raqamingizni ulashing.", ct);
                return;
            }

            if (!PhoneNumberHelper.TryNormalize(update.ContactPhone, out var phone))
            {
                await _bot.SendMessageAsync(chatId,
                    "❌ Raqam formati noto'g'ri. Ilovada ro'yxatdan o'tgan raqamingizni ulashing.", ct);
                return;
            }

            var user = await _users.GetByPhoneNumberAsync(phone);
            if (user is null)
            {
                await _bot.SendMessageAsync(chatId,
                    $"❌ <b>{phone}</b> raqami bilan hisob topilmadi. Avval ilovada ro'yxatdan o'ting.", ct);
                return;
            }

            // Havolada boshqa profil ko'rsatilgan bo'lsa — mos kelmasa bog'lamaymiz.
            if (_pendingProfiles.TryRemove(chatId, out var expectedUserId) && expectedUserId != user.Id)
            {
                _logger.LogWarning(
                    "[TG] Havoladagi profil ({Expected}) ulashilgan raqam profili ({Actual}) bilan mos emas.",
                    expectedUserId, user.Id);

                await _bot.SendMessageAsync(chatId,
                    "❌ Ulashilgan raqam ilovadagi hisob raqamiga to'g'ri kelmadi.", ct);
                return;
            }

            user.TelegramChatId = chatId;
            await _users.UpdateAsync(user);

            _logger.LogInformation("[TG] Chat bog'landi userId={UserId} chatId={ChatId}", user.Id, chatId);

            await _bot.SendMessageAsync(chatId,
                "✅ Hisob bog'landi.\n\nEndi ro'yxatdan o'tish va parolni tiklash kodlari shu yerga keladi.", ct);
        }

        // ── 3. Kod yuborish ──────────────────────────────────────────────────

        public async Task<bool> SendOtpAsync(string phoneNumber, string code, OtpPurpose purpose, CancellationToken ct = default)
        {
            if (!_options.Enabled || !_bot.IsConfigured) return false;

            var user = await _users.GetByPhoneNumberAsync(phoneNumber);
            if (user?.TelegramChatId is null) return false;

            var title = purpose switch
            {
                OtpPurpose.Register => "Ro'yxatdan o'tish",
                OtpPurpose.ResetPassword => "Parolni tiklash",
                _ => "Tasdiqlash"
            };

            var sent = await _bot.SendMessageAsync(
                user.TelegramChatId.Value,
                $"<b>{title}</b>\n\nTasdiqlash kodi: <code>{code}</code>\n\n" +
                "Kod qisqa muddat amal qiladi. Uni hech kimga aytmang.",
                ct);

            if (sent)
                _logger.LogInformation("[TG] Kod yuborildi userId={UserId} purpose={Purpose}", user.Id, purpose);
            else
                _logger.LogWarning("[TG] Kod yuborilmadi userId={UserId} purpose={Purpose}", user.Id, purpose);

            return sent;
        }

        private static string GenerateToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(12);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
