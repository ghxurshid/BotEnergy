namespace Domain.Enums
{
    /// <summary>
    /// Process qaysi manbadan moliyalashtiriladi. Start'da bir marta tanlanadi,
    /// settlement shu bo'yicha branch qiladi (ikkala yo'l ham TryClaimBalanceDeductionAsync
    /// claim'idan o'tadi — double-settle mumkin emas).
    /// </summary>
    public enum ProcessFundingSource
    {
        /// <summary>Ichki balans (CustomerUser/Organization.Balance) — legacy oqim.</summary>
        InternalBalance = 0,

        /// <summary>Sessiyaning hold invoice balansi (Payme pre-authorization), FIFO.</summary>
        HoldBalance = 1
    }
}
