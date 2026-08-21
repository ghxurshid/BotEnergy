namespace Domain.Enums
{
    /// <summary>
    /// Qurilma interfeysidagi naqd → karta sessiyasining holati.
    ///
    /// Asosiy oqim: <c>Accepting → Committing → Completed</c>.
    /// Bank yiqilsa <c>PayoutFailed</c> — watcher backoff bilan qayta urinadi,
    /// urinishlar tugagach admin qo'lda hal qiladi.
    ///
    /// <c>Cancelled</c> va <c>Expired</c> faqat pul solinmagan sessiyalar uchun:
    /// bill acceptor qabul qilingan pulni qaytara olmaydi, shuning uchun summa
    /// noldan katta bo'lsa sessiya har doim kartaga o'tkazish bilan yakunlanadi.
    /// </summary>
    public enum CashSessionStatus
    {
        /// <summary>Karta tasdiqlangan, qurilma naqd pul qabul qilmoqda.</summary>
        Accepting = 0,

        /// <summary>Mijoz "kartaga tushirilsin" dedi — bank payout bajarilmoqda.</summary>
        Committing = 1,

        /// <summary>Pul kartaga o'tdi va summa qurilmaning naqd qoldig'iga qo'shildi.</summary>
        Completed = 2,

        /// <summary>Payout yiqildi. Pul jismonan qurilmada, mijoz kartasiga hali o'tmagan.</summary>
        PayoutFailed = 3,

        /// <summary>Mijoz bekor qildi (faqat summa 0 bo'lganda mumkin).</summary>
        Cancelled = 4,

        /// <summary>Pul solinmasdan idle timeout bo'ldi.</summary>
        Expired = 5
    }
}
