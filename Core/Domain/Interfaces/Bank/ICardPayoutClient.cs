namespace Domain.Interfaces.Bank
{
    /// <summary>Karta tekshiruvi natijasi. To'liq PAN qaytarilmaydi.</summary>
    public sealed record CardVerification(string MaskedPan, string CardToken, string? HolderName);

    public enum CardPayoutState
    {
        Pending = 0,
        Succeeded = 1,
        Failed = 2
    }

    public sealed record CardPayout(string ProviderRef, CardPayoutState State);

    /// <summary>
    /// Naqd pulni mijozning bank kartasiga o'tkazuvchi tashqi servis.
    ///
    /// Hozircha <c>FakeCardPayoutClient</c> — haqiqiy bank integratsiyasi kelganda faqat
    /// implementatsiya almashadi, chaqiruvchi kod o'zgarmaydi.
    ///
    /// Konvensiya: metodlar HECH QACHON exception otmaydi, barcha xatolar
    /// <see cref="CardPayoutCall{T}"/> ichida qaytadi.
    ///
    /// <b>To'liq PAN</b> faqat <see cref="VerifyCardAsync"/> ga kiradi va undan nariga
    /// o'tmaydi: keyingi chaqiruvlar token bilan ishlaydi, log va bazaga faqat maska yoziladi.
    /// </summary>
    public interface ICardPayoutClient
    {
        /// <summary>
        /// Kartani tekshiradi va payout uchun token qaytaradi.
        /// </summary>
        Task<CardPayoutCall<CardVerification>> VerifyCardAsync(string pan, CancellationToken ct = default);

        /// <summary>
        /// Kartaga pul o'tkazadi. <paramref name="orderId"/> — bizning tomondan
        /// generatsiya qilingan unique identifikator (idempotentlik uchun bank tomonida ham).
        /// </summary>
        Task<CardPayoutCall<CardPayout>> PayoutAsync(
            string cardToken, decimal amountUzs, string orderId, CancellationToken ct = default);

        /// <summary>
        /// O'tkazma holatini so'raydi — javob noaniq qolgan holatlarda (timeout) ishlatiladi:
        /// pul allaqachon o'tgan bo'lishi mumkin, ikkinchi marta yuborib bo'lmaydi.
        /// </summary>
        Task<CardPayoutCall<CardPayout>> GetPayoutStatusAsync(
            string providerRef, CancellationToken ct = default);
    }
}
