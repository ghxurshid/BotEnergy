namespace Domain.Payments
{
    /// <summary>
    /// Provider chaqiruvining natijasi — retry qarori uchun tasnif.
    /// Payme'ga xos kodlar strategiya ichida shu qiymatlarga o'giriladi,
    /// umumiy yadro (PaymentStrategyBase) provider tafsilotlarini bilmaydi.
    /// </summary>
    public enum ProviderOutcome
    {
        /// <summary>Amal bajarildi.</summary>
        Success = 0,

        /// <summary>Hali kutilmoqda (mijoz to'lamagan) — holat o'zgarmaydi, poll davom etadi.</summary>
        Pending = 1,

        /// <summary>Vaqtinchalik xato (tarmoq/timeout/5xx) — backoff bilan qayta uriniladi.</summary>
        Transient = 2,

        /// <summary>Qaytarilmas xato — retry foydasiz, Failed + operator.</summary>
        Permanent = 3,

        /// <summary>Allaqachon bajarilgan — idempotent muvaffaqiyat.</summary>
        AlreadyDone = 4
    }

    /// <summary>
    /// Bitta provider chaqiruvi: natija + audit uchun xom payload'lar.
    /// Auth header'lari HECH QACHON payload'ga tushmaydi.
    /// </summary>
    public sealed record ProviderCall(
        ProviderOutcome Outcome,
        string? RequestPayload = null,
        string? ResponsePayload = null,
        string? Message = null)
    {
        public bool IsSuccess => Outcome is ProviderOutcome.Success or ProviderOutcome.AlreadyDone;

        public static ProviderCall Ok(string? message = null) => new(ProviderOutcome.Success, Message: message);

        /// <summary>Provider chaqiruvi shart bo'lmagan hollar uchun (prepaid capture kabi).</summary>
        public static ProviderCall NoOp(string message) => new(ProviderOutcome.AlreadyDone, Message: message);
    }

    /// <summary>Mablag' so'rash natijasi (intent yaratishda).</summary>
    /// <param name="Call">Chaqiruv natijasi va audit payload'lari.</param>
    /// <param name="ReceiptId">Provider chek identifikatori (bo'lsa).</param>
    /// <param name="TransactionId">Provider tranzaksiya identifikatori (Merchant API).</param>
    /// <param name="ProviderState">Provider holat kodi (bo'lsa).</param>
    /// <param name="CheckoutUrl">Mijoz ochishi kerak bo'lgan havola (bo'lsa).</param>
    /// <param name="AlreadyFunded">
    /// True — mablag' shu chaqiruvning o'zida ta'minlandi (Subscribe: saqlangan karta bilan
    /// to'landi). Bunda intent WaitingForConfirmation'ni chetlab Funded bo'ladi.
    /// </param>
    public sealed record ProviderFunding(
        ProviderCall Call,
        string? ReceiptId = null,
        string? TransactionId = null,
        int? ProviderState = null,
        string? CheckoutUrl = null,
        bool AlreadyFunded = false);

    /// <summary>Polling natijasi: mijoz to'ladimi.</summary>
    public enum FundingPollState
    {
        /// <summary>Hali to'lanmagan.</summary>
        Pending = 0,

        /// <summary>Mablag' ta'minlandi (hold ushlandi yoki to'lov o'tdi).</summary>
        Funded = 1,

        /// <summary>Provider tomonda bekor qilingan.</summary>
        Cancelled = 2
    }

    public sealed record ProviderPoll(
        ProviderCall Call,
        FundingPollState State = FundingPollState.Pending,
        int? ProviderState = null);
}
