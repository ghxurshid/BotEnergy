using Domain.Dtos.Base;
using Domain.Dtos.PaymentSession;
using Domain.Entities;

namespace Domain.Interfaces
{
    /// <summary>
    /// Sessiyaga bog'langan to'lov konteksti (PaymentSession) boshqaruvi.
    /// SessionApi-only — DeviceSessionService connect'da yaratadi.
    /// </summary>
    public interface IPaymentSessionService
    {
        /// <summary>Device sessiyasi ochilganda payment session yaratadi (balance=0, Active).</summary>
        Task<PaymentSessionEntity> CreateForSessionAsync(long sessionId, long deviceId, long userId, long merchantId);

        /// <summary>Sessiya balans holati + invoice ro'yxati (mobil uchun).</summary>
        Task<GenericDto<PaymentSessionDto>> GetForSessionAsync(long sessionId, long userId);
    }
}
