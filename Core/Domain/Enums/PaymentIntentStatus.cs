namespace Domain.Enums
{
    /// <summary>
    /// To'lov niyati (payment intent) hayot davri — uchala strategiya uchun bir xil.
    /// Ruxsat etilgan o'tishlar FAQAT <c>PaymentIntentStateMachine</c> jadvalida —
    /// statuslar hech qachon to'g'ridan-to'g'ri yozilmaydi
    /// (<c>IPaymentIntentRepository.TryTransitionAsync</c> yagona yozish nuqtasi).
    ///
    /// Raqamli qiymatlar DB'da saqlanadi — HECH QACHON o'zgartirilmaydi.
    /// </summary>
    public enum PaymentIntentStatus
    {
        /// <summary>Yozuv yaratildi, provider tomonda hali hech narsa yo'q.</summary>
        Created = 0,

        /// <summary>
        /// Provider tomonda chek/tranzaksiya ochildi, mijoz to'lovi kutilmoqda.
        /// Subscribe: hold receipt; Invoice: mijozga yuborilgan chek; Merchant: checkout link.
        /// </summary>
        WaitingForConfirmation = 1,

        /// <summary>
        /// Mablag' ta'minlandi va sessiya balansiga qo'shildi.
        /// Subscribe: pul Payme'da USHLANGAN (hold); Invoice/Merchant: pul allaqachon YECHILGAN.
        /// </summary>
        Funded = 2,

        /// <summary>Ta'minlangan mablag'ning bir qismi dispense'ga ishlatilgan.</summary>
        PartiallyConsumed = 3,

        /// <summary>Ta'minlangan mablag' to'liq ishlatilgan.</summary>
        FullyConsumed = 4,

        /// <summary>
        /// Yakuniy hisob-kitob maqsadi qo'yilgan (CaptureAmountTiyin) — watcher bajaradi.
        /// Subscribe: confirm_hold; prepaid strategiyalarda bu holat ishlatilmaydi
        /// (pul allaqachon yechilgan — to'g'ridan Settled bo'ladi).
        /// </summary>
        SettlePending = 5,

        /// <summary>Moliyaviy yakunlandi: ishlatilgan summa merchantda qoldi.</summary>
        Settled = 6,

        /// <summary>Qaytarish maqsadi qo'yilgan — watcher bajaradi (hold bo'shatish yoki real refund).</summary>
        RefundPending = 7,

        /// <summary>Ishlatilmagan mablag' mijozga qaytarildi.</summary>
        Refunded = 8,

        /// <summary>To'lovgacha bekor qilindi.</summary>
        Cancelled = 9,

        /// <summary>Mijoz TTL ichida to'lamadi — chek bekor qilindi.</summary>
        Expired = 10,

        /// <summary>Qaytarilmas xato yoki retry limiti tugadi — operator aralashuvi kerak.</summary>
        Failed = 11
    }
}
