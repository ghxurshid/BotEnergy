using Application.Services;
using Domain.Enums;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CommonConfiguration.Redis
{
    /// <summary>
    /// Redis primary + in-memory fallback (boshqa Resilient* store'lar bilan bir xil naqsh).
    ///
    /// Redis yiqilganda OTP butunlay ishlamay qolishi — login/registratsiyaning to'liq to'xtashi
    /// degani. Fallback bilan tizim bitta instansiya rejimida ishlashda davom etadi: kod
    /// xotirada yaratiladi va xotiradan tekshiriladi.
    ///
    /// Tekshiruv har doim ikkala manbadan qidiriladi — kod Redis ishlab turganda yaratilib,
    /// tekshiruv paytida Redis yiqilgan bo'lishi mumkin (va aksincha).
    /// </summary>
    public sealed class ResilientOtpService : IOtpService
    {
        private readonly RedisOtpService _primary;
        private readonly OtpService _fallback;
        private readonly ILogger<ResilientOtpService> _logger;

        public ResilientOtpService(
            RedisOtpService primary,
            OtpService fallback,
            ILogger<ResilientOtpService> logger)
        {
            _primary = primary;
            _fallback = fallback;
            _logger = logger;
        }

        public async Task<string> GenerateOtpAsync(string phoneNumber, OtpPurpose purpose)
        {
            try
            {
                var code = await _primary.GenerateOtpAsync(phoneNumber, purpose);
                return code;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis OTP generate failed, using in-memory fallback.");
                return await _fallback.GenerateOtpAsync(phoneNumber, purpose);
            }
        }

        public async Task<bool> VerifyOtpAsync(string phoneNumber, string code, OtpPurpose purpose)
        {
            try
            {
                if (await _primary.VerifyOtpAsync(phoneNumber, code, purpose))
                    return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis OTP verify failed, using in-memory fallback.");
            }

            return await _fallback.VerifyOtpAsync(phoneNumber, code, purpose);
        }

        public async Task<bool> IsOtpVerifiedAsync(string phoneNumber, OtpPurpose purpose)
        {
            try
            {
                if (await _primary.IsOtpVerifiedAsync(phoneNumber, purpose))
                    return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis OTP verified-check failed, using in-memory fallback.");
            }

            return await _fallback.IsOtpVerifiedAsync(phoneNumber, purpose);
        }

        public async Task ConsumeOtpVerificationAsync(string phoneNumber, OtpPurpose purpose)
        {
            try
            {
                await _primary.ConsumeOtpVerificationAsync(phoneNumber, purpose);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis OTP consume failed, continuing with in-memory fallback.");
            }

            await _fallback.ConsumeOtpVerificationAsync(phoneNumber, purpose);
        }
    }
}
