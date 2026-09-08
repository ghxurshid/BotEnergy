using Domain.Auth;
using Domain.Dtos.Base;
using Domain.Dtos.PaymentSession;
using Domain.Enums;

namespace Domain.Interfaces
{
    /// <summary>
    /// Operator (AdminApi) to'lov boshqaruvi — usuldan qat'i nazar. Provider'ni CHAQIRMAYDI:
    /// faqat maqsad holat qo'yadi, ijroni SessionApi watcher'i tegishli strategiya orqali bajaradi.
    /// Har amal OperatorAction step bilan audit qilinadi.
    /// Repo'largagina bog'liq — shuning uchun RegisterServices'da (barcha API) ro'yxatga olinadi.
    /// </summary>
    public interface IPaymentIntentAdminService
    {
        Task<GenericDto<List<PaymentIntentAdminItemDto>>> ListAsync(
            int skip, int take, long? merchantId, long? sessionId,
            PaymentIntentStatus? status, PaymentMethod? method, DateTime? from, DateTime? to, AccessScope scope);

        Task<GenericDto<PaymentIntentAdminItemDto>> GetByIdAsync(long intentId, AccessScope scope);
        Task<GenericDto<List<PaymentIntentStepItemDto>>> GetStepsAsync(long intentId, AccessScope scope);

        Task<GenericDto<PaymentIntentAdminItemDto>> ForceCaptureAsync(long intentId, PaymentIntentOperatorActionDto dto, long adminUserId, AccessScope scope);
        Task<GenericDto<PaymentIntentAdminItemDto>> ForceRefundAsync(long intentId, PaymentIntentOperatorActionDto dto, long adminUserId, AccessScope scope);
        Task<GenericDto<PaymentIntentAdminItemDto>> ForceCancelAsync(long intentId, PaymentIntentOperatorActionDto dto, long adminUserId, AccessScope scope);
        Task<GenericDto<PaymentIntentAdminItemDto>> RetryAsync(long intentId, PaymentIntentOperatorActionDto dto, long adminUserId, AccessScope scope);
    }
}
