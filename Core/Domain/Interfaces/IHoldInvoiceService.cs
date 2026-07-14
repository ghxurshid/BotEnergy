using Domain.Dtos.Base;
using Domain.Dtos.PaymentSession;

namespace Domain.Interfaces
{
    /// <summary>
    /// Hold invoice yaratish/bekor qilish (mobil oqim). SessionApi-only.
    /// Payme settlement (capture/refund) bu yerda EMAS — watcher bajaradi.
    /// </summary>
    public interface IHoldInvoiceService
    {
        Task<GenericDto<HoldInvoiceResultDto>> CreateAsync(CreateHoldInvoiceDto dto, CancellationToken ct = default);

        /// <summary>Faqat umuman ishlatilmagan (Consumed=0) invoice'ni bekor qilish.</summary>
        Task<GenericDto<HoldInvoiceResultDto>> CancelByUserAsync(long invoiceId, long userId, CancellationToken ct = default);

        Task<GenericDto<List<HoldInvoiceItemDto>>> GetForSessionAsync(long sessionId, long userId);
    }
}
