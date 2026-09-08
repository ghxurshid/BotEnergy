using Domain.Dtos.Base;
using Domain.Dtos.PaymentSession;
using Domain.Entities;
using Domain.Payments;

namespace Domain.Interfaces
{
    /// <summary>
    /// Sessiyaga bog'langan to'lov konteksti (PaymentSession) boshqaruvi.
    /// SessionApi-only — DeviceSessionService connect'da yaratadi.
    ///
    /// To'lov usuli SHU YERDA bir marta tanlanadi (merchant sozlamasidan) va kontekstga
    /// qotiriladi — merchant keyin usulni almashtirsa, ochiq sessiya eskisi bilan yakunlanadi.
    /// </summary>
    public interface IPaymentSessionService
    {
        /// <summary>
        /// Device sessiyasi ochilganda payment session yaratadi (balance=0, Active).
        /// Idempotent: mavjud bo'lsa o'shani qaytaradi.
        /// </summary>
        Task<PaymentSessionEntity> CreateForSessionAsync(long sessionId, long deviceId, long userId, long merchantId);

        /// <summary>
        /// To'lov kontekstini kafolatlaydi: mavjud bo'lsa qaytaradi, aks holda sessiyaning
        /// qurilmasi va uning merchant sozlamasi bo'yicha yaratadi (lazy-create).
        /// </summary>
        Task<EnsurePaymentSessionResult> EnsureForSessionAsync(long sessionId);

        /// <summary>Sessiya balans holati + to'lov ro'yxati (mobil uchun).</summary>
        Task<GenericDto<PaymentSessionDto>> GetForSessionAsync(long sessionId, long userId);
    }
}
