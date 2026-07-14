using Domain.Enums;

namespace Domain.Dtos.PaymentSession
{
    public class HoldInvoiceAdminItemDto
    {
        public long InvoiceId { get; set; }
        public long PaymentSessionId { get; set; }
        public long SessionId { get; set; }
        public long MerchantId { get; set; }
        public long DeviceId { get; set; }
        public long UserId { get; set; }
        public int SequenceNo { get; set; }
        public HoldInvoiceStatus Status { get; set; }
        public long AmountTiyin { get; set; }
        public long ConsumedTiyin { get; set; }
        public long? CaptureAmountTiyin { get; set; }
        public string? ProviderReceiptId { get; set; }
        public int? ProviderState { get; set; }
        public int AttemptCount { get; set; }
        public DateTime? NextAttemptAt { get; set; }
        public string? FailureReason { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? HoldAt { get; set; }
        public DateTime? SettledAt { get; set; }
    }

    public class HoldInvoiceStepItemDto
    {
        public long Id { get; set; }
        public HoldInvoiceStepType StepType { get; set; }
        public PaymentStepStatus Status { get; set; }
        public string? RequestPayload { get; set; }
        public string? ResponsePayload { get; set; }
        public string? Message { get; set; }
        public Guid CorrelationId { get; set; }
        public DateTime OccurredAt { get; set; }
    }

    /// <summary>Operator amali uchun — reason (audit) va ixtiyoriy summa.</summary>
    public class HoldInvoiceOperatorActionDto
    {
        public decimal? AmountUzs { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
