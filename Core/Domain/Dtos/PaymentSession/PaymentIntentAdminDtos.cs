using Domain.Enums;

namespace Domain.Dtos.PaymentSession
{
    public class PaymentIntentAdminItemDto
    {
        public long IntentId { get; set; }
        public long PaymentSessionId { get; set; }
        public long SessionId { get; set; }
        public long MerchantId { get; set; }
        public long DeviceId { get; set; }
        public long UserId { get; set; }
        public int SequenceNo { get; set; }
        public PaymentIntentStatus Status { get; set; }
        public PaymentMethod Method { get; set; }
        public PaymentIntentKind Kind { get; set; }
        public long AmountTiyin { get; set; }
        public long ConsumedTiyin { get; set; }
        public long? CaptureAmountTiyin { get; set; }
        public string? ProviderReceiptId { get; set; }
        public string? ProviderTransactionId { get; set; }
        public int? ProviderState { get; set; }
        public int AttemptCount { get; set; }
        public DateTime? NextAttemptAt { get; set; }
        public string? FailureReason { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? FundedAt { get; set; }
        public DateTime? SettledAt { get; set; }

        /// <summary>
        /// Prepaid usullarda (Invoice/Merchant) mijozga QAYTARILMAGAN qoldiq (tiyin):
        /// pul allaqachon yechilgan, chek esa qisman qaytarilmaydi (Payme cheklovi).
        /// 0 — qoldiq yo'q yoki hold usuli (u yerda ishlatilmagan qism o'zi bo'shaydi).
        /// Moliyaviy hisobot va operator qarori uchun.
        /// </summary>
        public long UnrefundedRemainderTiyin { get; set; }
    }

    public class PaymentIntentStepItemDto
    {
        public long Id { get; set; }
        public PaymentIntentStepType StepType { get; set; }
        public PaymentStepStatus Status { get; set; }
        public string? RequestPayload { get; set; }
        public string? ResponsePayload { get; set; }
        public string? Message { get; set; }
        public Guid CorrelationId { get; set; }
        public DateTime OccurredAt { get; set; }
    }

    /// <summary>Operator amali uchun — reason (audit) va ixtiyoriy summa.</summary>
    public class PaymentIntentOperatorActionDto
    {
        public decimal? AmountUzs { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
