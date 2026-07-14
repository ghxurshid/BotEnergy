using Domain.Auth;
using Domain.Dtos.Base;
using Domain.Dtos.PaymentSession;
using Domain.Enums;

namespace Domain.Interfaces
{
    /// <summary>
    /// Operator (AdminApi) hold invoice boshqaruvi. Payme'ni CHAQIRMAYDI — faqat maqsad holat
    /// qo'yadi (SessionApi watcher bajaradi). Har amal OperatorAction step bilan audit qilinadi.
    /// Repo'largagina bog'liq — shuning uchun RegisterServices'da (barcha API) ro'yxatga olinadi.
    /// </summary>
    public interface IHoldInvoiceAdminService
    {
        Task<GenericDto<List<HoldInvoiceAdminItemDto>>> ListAsync(
            int skip, int take, long? merchantId, long? sessionId,
            HoldInvoiceStatus? status, DateTime? from, DateTime? to, AccessScope scope);

        Task<GenericDto<HoldInvoiceAdminItemDto>> GetByIdAsync(long invoiceId, AccessScope scope);
        Task<GenericDto<List<HoldInvoiceStepItemDto>>> GetStepsAsync(long invoiceId, AccessScope scope);

        Task<GenericDto<HoldInvoiceAdminItemDto>> ForceCaptureAsync(long invoiceId, HoldInvoiceOperatorActionDto dto, long adminUserId, AccessScope scope);
        Task<GenericDto<HoldInvoiceAdminItemDto>> ForceRefundAsync(long invoiceId, HoldInvoiceOperatorActionDto dto, long adminUserId, AccessScope scope);
        Task<GenericDto<HoldInvoiceAdminItemDto>> ForceCancelAsync(long invoiceId, HoldInvoiceOperatorActionDto dto, long adminUserId, AccessScope scope);
        Task<GenericDto<HoldInvoiceAdminItemDto>> RetryAsync(long invoiceId, HoldInvoiceOperatorActionDto dto, long adminUserId, AccessScope scope);
    }
}
