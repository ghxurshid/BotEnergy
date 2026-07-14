using Domain.Entities.BaseEntity;
using Domain.Enums;

namespace Domain.Entities
{
    /// <summary>
    /// Payme hold (pre-authorization) invoice. Status faqat HoldInvoiceStateMachine
    /// ruxsat jadvali bo'yicha, IHoldInvoiceRepository.TryTransitionAsync orqali o'zgaradi.
    /// Watcher (SessionApi) barcha Payme settlement chaqiruvlarini bajaradi;
    /// boshqa oqimlar faqat maqsad holat (CapturePending/RefundPending) qo'yadi.
    /// Barcha summalar integer TIYIN.
    /// </summary>
    public class HoldInvoiceEntity : Entity
    {
        public long PaymentSessionId { get; set; }
        public PaymentSessionEntity? PaymentSession { get; set; }

        /// <summary>FIFO tartib — sessiya ichida 1 dan boshlab o'sadi. N to'liq tugamaguncha N+1 ga tegilmaydi.</summary>
        public int SequenceNo { get; set; }

        /// <summary>Ushlab turiladigan summa (tiyin).</summary>
        public long AmountTiyin { get; set; }

        /// <summary>Dispense'larga ishlatilgan qism (tiyin). Amount'dan oshmaydi.</summary>
        public long ConsumedTiyin { get; set; }

        /// <summary>CapturePending'da yechiladigan summa (tiyin) — odatda ConsumedTiyin.
        /// Payme confirm_hold(amount) qisman yechadi, qolgani avtomatik bo'shaydi.</summary>
        public long? CaptureAmountTiyin { get; set; }

        public HoldInvoiceStatus Status { get; set; } = HoldInvoiceStatus.Created;

        /// <summary>Payme receipt _id.</summary>
        public string? ProviderReceiptId { get; set; }

        /// <summary>Bizning order_id — providerga yuboriladi. Unique.</summary>
        public string ProviderOrderId { get; set; } = string.Empty;

        /// <summary>Payme receipt state raqami (oxirgi ko'rilgan).</summary>
        public int? ProviderState { get; set; }

        public string? IdempotencyKey { get; set; }

        public long CreatedByUserId { get; set; }

        public string? FailureReason { get; set; }

        // ── Watcher retry/lease maydonlari ──────────────────────────
        public int AttemptCount { get; set; }

        /// <summary>Watcher keyingi urinish vaqti. NULL — navbatda emas.</summary>
        public DateTime? NextAttemptAt { get; set; }

        /// <summary>Lease egasi (instance id) — parallel tick/instance'lar bir invoice'ni ikki marta olmasligi uchun.</summary>
        public string? LockedBy { get; set; }

        public DateTime? LeaseUntil { get; set; }

        public DateTime? HoldAt { get; set; }
        public DateTime? SettledAt { get; set; }

        /// <summary>Optimistic concurrency (PostgreSQL xmin).</summary>
        public uint RowVersion { get; set; }

        public ICollection<HoldInvoiceStepEntity>? Steps { get; set; }
    }
}
