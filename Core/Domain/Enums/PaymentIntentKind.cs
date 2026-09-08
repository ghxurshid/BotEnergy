namespace Domain.Enums
{
    /// <summary>
    /// Intent mablag'ni qanday ushlab turishi. Raqamli qiymatlar DB'da saqlanadi.
    /// </summary>
    public enum PaymentIntentKind
    {
        /// <summary>
        /// Pul mijoz kartasida USHLANGAN (pre-authorization), yakunda ishlatilgani yechiladi.
        /// Faqat <see cref="PaymentMethod.Subscribe"/> qo'llaydi.
        /// </summary>
        Hold = 0,

        /// <summary>
        /// Pul darhol YECHILGAN. Yakunda ishlatilmagan qism qaytariladi (refund).
        /// <see cref="PaymentMethod.Invoice"/> va <see cref="PaymentMethod.Merchant"/>.
        /// </summary>
        Charge = 1
    }
}
