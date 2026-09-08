using Domain.Enums;

namespace Domain.Payments
{
    /// <summary>
    /// Strategiya nimani qo'llay olishi. Amal boshlanishidan oldin tekshiriladi —
    /// qo'llamaydigan amal stop-factor bilan rad etiladi (masalan hold faqat Subscribe'da).
    /// </summary>
    [Flags]
    public enum PaymentCapabilities
    {
        None = 0,

        /// <summary>Pre-authorization: pul ushlanadi, yakunda ishlatilgani yechiladi.</summary>
        Hold = 1 << 0,

        /// <summary>Ushlangan summaning bir qismini yechish (confirm_hold(amount)).</summary>
        PartialCapture = 1 << 1,

        /// <summary>Yechilgan pulni qaytarish.</summary>
        Refund = 1 << 2,

        /// <summary>Qisman qaytarish (chekning bir qismini) — Payme Receipts API qo'llamaydi.</summary>
        PartialRefund = 1 << 3,

        /// <summary>Provider bizga callback qiladi (Merchant API) — polling shart emas.</summary>
        ProviderCallback = 1 << 4,

        /// <summary>Saqlangan karta tokeni bilan server tomondan to'lash.</summary>
        SavedCard = 1 << 5
    }

    /// <summary>
    /// Sessiya yopilganda ta'minlangan mablag' bilan nima bo'ladi.
    /// </summary>
    public enum SettlementMode
    {
        /// <summary>Ushlangan puldan ishlatilgani yechiladi, qolgani bo'shatiladi (Hold).</summary>
        CaptureOnClose = 0,

        /// <summary>Pul allaqachon yechilgan — ishlatilmagan qism qaytariladi (Charge).</summary>
        RefundRemainderOnClose = 1,

        /// <summary>Qaytarish yo'q — ishlatilmagan mablag' merchantda qoladi.</summary>
        None = 2
    }

    /// <summary>
    /// Strategiya "pasporti" — resolver va stop-factor tekshiruvlari shu ma'lumot bilan ishlaydi.
    /// </summary>
    /// <param name="Method">Qaysi usul.</param>
    /// <param name="Kind">Mablag' hold qilinadimi yoki darhol yechiladimi.</param>
    /// <param name="Capabilities">Nimani qo'llaydi.</param>
    /// <param name="Settlement">Yopilishda pul bilan nima bo'ladi.</param>
    public sealed record PaymentStrategyProfile(
        PaymentMethod Method,
        PaymentIntentKind Kind,
        PaymentCapabilities Capabilities,
        SettlementMode Settlement)
    {
        public bool Supports(PaymentCapabilities capability) => Capabilities.HasFlag(capability);
    }
}
