using Domain.Enums;

namespace Domain.Dtos.Cash
{
    /// <summary>Karta tasdiqlanib sessiya ochilgani — qurilmaga qaytariladi.</summary>
    public sealed class CashSessionOpenedDto
    {
        public long CashSessionId { get; set; }
        public string CardMasked { get; set; } = string.Empty;
    }

    /// <summary>Kupyura qabul qilingandan keyingi holat — qurilma ekranida shu ko'rsatiladi.</summary>
    public sealed class CashSessionTotalDto
    {
        public long CashSessionId { get; set; }
        public decimal AcceptedTotal { get; set; }
        public int BillCount { get; set; }

        /// <summary>
        /// <c>false</c> — shu kupyura allaqachon hisobga olingan (takroriy xabar).
        /// Xato emas: summa o'zgarmaydi, qurilma haqiqiy jamini oladi.
        /// </summary>
        public bool Added { get; set; }
    }

    /// <summary>Commit natijasi — kartaga o'tkazish yakunlandimi yoki kutilmoqdami.</summary>
    public sealed class CashSessionResultDto
    {
        public long CashSessionId { get; set; }
        public CashSessionStatus Status { get; set; }
        public decimal Amount { get; set; }
        public string? PayoutReference { get; set; }
        public string? Message { get; set; }

        /// <summary>Qurilmaning yangi naqd qoldig'i (faqat muvaffaqiyatli commit'da to'ldiriladi).</summary>
        public decimal? DeviceCashBalance { get; set; }

        /// <summary>
        /// <c>PayoutFailed</c> holatida: qayta urinish rejalashtirilganmi.
        /// <c>true</c> — vaqtinchalik xato, natija keyin push qilinadi;
        /// <c>false</c> — yakuniy xato, operator aralashuvi kerak.
        /// Qurilma shu bayroqqa qarab ekranda "kutilmoqda" yoki "xato" ko'rsatadi.
        /// </summary>
        public bool RetryScheduled { get; set; }
    }
}
