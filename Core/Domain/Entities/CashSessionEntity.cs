using Domain.Attributes;
using Domain.Entities.BaseEntity;
using Domain.Enums;

namespace Domain.Entities
{
    /// <summary>
    /// Qurilma interfeysidagi naqd → karta to'ldirish sessiyasi.
    ///
    /// Bu <see cref="SessionEntity"/> EMAS: u yerda mobil ilovaga kirgan foydalanuvchi bo'ladi,
    /// bu yerda esa foydalanuvchi akkaunti umuman yo'q — mijoz kolonka ekranida faqat karta
    /// raqamini kiritadi. Shuning uchun <c>UserId</c> ham yo'q.
    ///
    /// <b>To'liq PAN saqlanmaydi.</b> Faqat maskalangan ko'rinish va bankdan qaytgan token
    /// yoziladi; 16 xonali raqam faqat karta tekshiruvi davomida xotirada bo'ladi.
    /// </summary>
    public class CashSessionEntity : Entity
    {
        public long DeviceId { get; set; }
        public DeviceEntity? Device { get; set; }

        /// <summary>Denormalizatsiya — audit va MQTT korrelyatsiyasi join'siz ishlashi uchun.</summary>
        public required string SerialNumber { get; set; }

        /// <summary>Mijozga ko'rsatiladigan maskalangan karta, masalan <c>8600****1234</c>.</summary>
        public required string CardMasked { get; set; }

        /// <summary>Bankdan qaytgan karta tokeni — payout shu token bilan bajariladi.</summary>
        [NotSearchable]
        public required string CardToken { get; set; }

        public CashSessionStatus Status { get; set; } = CashSessionStatus.Accepting;

        /// <summary>Qabul qilingan naqd pul jami (UZS). Faqat kupyura qo'shilganda o'zgaradi.</summary>
        public decimal AcceptedAmount { get; set; }

        /// <summary>Qabul qilingan kupyuralar soni — <c>Bills</c> ni yuklamasdan ko'rsatish uchun.</summary>
        public int BillCount { get; set; }

        public string Currency { get; set; } = "UZS";

        /// <summary>Bankdagi payout identifikatori (muvaffaqiyatli yoki urinilgan).</summary>
        public string? PayoutReference { get; set; }

        /// <summary>Commit so'rovining idempotentlik kaliti — takroriy commit ikkinchi payout yasamaydi.</summary>
        public string? IdempotencyKey { get; set; }

        public string? FailureReason { get; set; }

        public DateTime? CompletedAt { get; set; }

        /// <summary>Idle timeout uchun — har kupyura va har buyruqda yangilanadi.</summary>
        public DateTime LastActivityAt { get; set; } = DateTime.Now;

        // ── Watcher retry (HoldInvoiceEntity bilan bir xil andoza) ──────────────
        public int AttemptCount { get; set; }
        public DateTime? NextAttemptAt { get; set; }

        /// <summary>Lease egasi (instance id) — ikki instansiya bir sessiyani olmasligi uchun.</summary>
        public string? LockedBy { get; set; }
        public DateTime? LeaseUntil { get; set; }

        public ICollection<CashSessionBillEntity>? Bills { get; set; }
    }
}
