namespace Domain.Enums
{
    /// <summary>
    /// Process qaysi manbadan moliyalashtiriladi. Start'da bir marta yoziladi,
    /// settlement shu bo'yicha yo'naltiriladi (ikkala yo'l ham TryClaimBalanceDeductionAsync
    /// claim'idan o'tadi — double-settle mumkin emas).
    ///
    /// Raqamli qiymatlar DB'da saqlanadi — o'zgartirilmaydi.
    /// </summary>
    public enum ProcessFundingSource
    {
        /// <summary>Ichki balans (CustomerUser/Organization.Balance) — LEGACY, faqat eski yozuvlar.</summary>
        InternalBalance = 0,

        /// <summary>
        /// Sessiyaning to'lov konteksti (PaymentSession) — konkret usul (Merchant/Invoice/
        /// Subscribe) PaymentSession.Method da saqlanadi, bu yerda takrorlanmaydi.
        /// </summary>
        SessionPayment = 1
    }
}
