using Domain.Enums;
using Domain.Interfaces;
using Domain.Interfaces.Telegram;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
    /// <summary>
    /// OTP servisining ustiga qo'yiladigan qatlam: kod generatsiya qilingach uni
    /// foydalanuvchining Telegram chatiga yuboradi.
    ///
    /// Nega dekorator: kodning o'zi faqat shu yerda "qo'lda" bo'ladi —
    /// <c>AuthService</c> uni ishlatmaydi va qaytarilgan qiymatni tashlab yuboradi.
    /// Yuborish muvaffaqiyatsiz bo'lsa oqim buzilmaydi: kod baribir yaratilgan,
    /// foydalanuvchi uni qayta so'rashi yoki boshqa kanal qo'shilishi mumkin.
    /// </summary>
    public sealed class TelegramNotifyingOtpService : IOtpService
    {
        private readonly IOtpService _inner;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TelegramNotifyingOtpService> _logger;

        public TelegramNotifyingOtpService(
            IOtpService inner,
            IServiceScopeFactory scopeFactory,
            ILogger<TelegramNotifyingOtpService> logger)
        {
            _inner = inner;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task<string> GenerateOtpAsync(string phoneNumber, OtpPurpose purpose)
        {
            var code = await _inner.GenerateOtpAsync(phoneNumber, purpose);

            try
            {
                // Repozitoriylar scoped — singleton servis ichida scope ochamiz.
                using var scope = _scopeFactory.CreateScope();
                var telegram = scope.ServiceProvider.GetService<ITelegramGateway>();

                if (telegram is not null)
                    await telegram.SendOtpAsync(phoneNumber, code, purpose);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[TG] Kodni yuborishda xatolik (oqim to'xtatilmadi).");
            }

            return code;
        }

        public Task<bool> VerifyOtpAsync(string phoneNumber, string code, OtpPurpose purpose)
            => _inner.VerifyOtpAsync(phoneNumber, code, purpose);

        public Task<bool> IsOtpVerifiedAsync(string phoneNumber, OtpPurpose purpose)
            => _inner.IsOtpVerifiedAsync(phoneNumber, purpose);

        public Task ConsumeOtpVerificationAsync(string phoneNumber, OtpPurpose purpose)
            => _inner.ConsumeOtpVerificationAsync(phoneNumber, purpose);
    }
}
