namespace Domain.Interfaces.Payme
{
    /// <summary>
    /// Payme receipt <c>state</c> kodlari (Subscribe API / HOLDPAY hujjati bo'yicha).
    /// DIQQAT: hold oqimidagi real kodlarni sandbox'da tasdiqlang — ayrim oraliq
    /// kodlar (20/21/30) kassa sozlamasiga qarab farq qilishi mumkin.
    /// </summary>
    public static class PaymeReceiptStates
    {
        /// <summary>Chek yaratildi, to'lov kutilmoqda.</summary>
        public const int Created = 0;

        /// <summary>Pul yechildi (to'liq yoki confirm_hold'dan keyin).</summary>
        public const int Paid = 4;

        /// <summary>Mijoz to'ladi — pul ushlab turilibdi (hold).</summary>
        public const int Held = 5;

        /// <summary>Bekor qilindi / qaytarildi.</summary>
        public const int Cancelled = 50;
    }

    /// <summary>
    /// Payme xato kodlari — retry qarorlari uchun.
    /// </summary>
    public static class PaymeErrorCodes
    {
        /// <summary>X-Auth noto'g'ri / privilegiya yetarli emas.</summary>
        public const int NotEnoughPrivileges = -32504;

        /// <summary>Chek topilmadi.</summary>
        public const int ReceiptNotFound = -31601;

        /// <summary>Chek holati amalga mos emas (masalan hold bo'lmaganda confirm_hold).</summary>
        public const int ReceiptStateMismatch = -31630;

        /// <summary>Buyurtma/account topilmadi.</summary>
        public const int AccountNotFound = -31611;

        /// <summary>Summa chegaradan tashqari.</summary>
        public const int AmountOutOfRange = -31001;
    }
}
