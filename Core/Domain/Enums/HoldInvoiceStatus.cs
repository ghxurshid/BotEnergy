namespace Domain.Enums
{
    /// <summary>
    /// Hold invoice (Payme pre-authorization) hayot davri. Ruxsat etilgan o'tishlar
    /// FAQAT <c>HoldInvoiceStateMachine</c> jadvalida — statuslar hech qachon to'g'ridan-to'g'ri yozilmaydi.
    /// </summary>
    public enum HoldInvoiceStatus
    {
        /// <summary>Yozuv yaratildi, Payme receipt hali yo'q.</summary>
        Created = 0,

        /// <summary>Payme receipt yaratildi (hold:true), mijoz to'lovini kutmoqda.</summary>
        WaitingForConfirmation = 1,

        /// <summary>Mijoz to'ladi — pul Payme'da ushlangan, sessiya balansiga qo'shilgan.</summary>
        Hold = 2,

        /// <summary>Hold mablag'ining bir qismi dispense'ga ishlatilgan.</summary>
        PartiallyConsumed = 3,

        /// <summary>Hold mablag'i to'liq ishlatilgan.</summary>
        FullyConsumed = 4,

        /// <summary>Capture maqsadi qo'yilgan (CaptureAmountTiyin) — watcher confirm_hold bajaradi.</summary>
        CapturePending = 5,

        /// <summary>Ishlatilgan summa yechildi (qisman capture'da qolgani avtomatik bo'shatildi).</summary>
        Captured = 6,

        /// <summary>Refund maqsadi qo'yilgan — watcher receipts.cancel bajaradi.</summary>
        RefundPending = 7,

        /// <summary>Ushlab turilgan pul to'liq qaytarildi.</summary>
        Refunded = 8,

        /// <summary>To'lovgacha bekor qilindi (receipt cancel).</summary>
        Cancelled = 9,

        /// <summary>Mijoz TTL ichida to'lamadi — receipt bekor qilindi.</summary>
        Expired = 10,

        /// <summary>Qaytarilmas xato yoki retry limiti tugadi — operator aralashuvi kerak.</summary>
        Failed = 11
    }
}
