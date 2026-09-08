namespace Domain.Enums
{
    /// <summary>
    /// Payment intent audit trail qadamlari (append-only, <c>payment_intent_steps</c>).
    /// Raqamli qiymatlar DB'da saqlanadi — o'zgartirilmaydi.
    /// </summary>
    public enum PaymentIntentStepType
    {
        Initiated = 0,
        Validated = 1,

        /// <summary>Provider'dan mablag' so'raldi (receipts.create / checkout link).</summary>
        FundingRequested = 2,

        /// <summary>Provider chek/tranzaksiya yaratdi.</summary>
        FundingCreated = 3,

        /// <summary>Chek mijozga yetkazildi (SMS / deep-link / QR).</summary>
        DeliveryRequested = 4,

        CheckPolled = 5,

        /// <summary>Mablag' ta'minlandi (hold ushlandi yoki to'lov o'tdi).</summary>
        Funded = 6,

        ConsumeApplied = 7,
        SettlementTargetAssigned = 8,
        CaptureRequested = 9,
        CaptureResponded = 10,
        RefundRequested = 11,
        RefundResponded = 12,
        Cancelled = 13,
        Expired = 14,
        Failed = 15,
        OperatorAction = 16,
        Retry = 17,

        /// <summary>Provider bizga callback yubordi (Merchant API).</summary>
        ProviderCallback = 18
    }
}
