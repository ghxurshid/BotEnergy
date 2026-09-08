using Domain.Entities.BaseEntity;
using Domain.Enums;

namespace Domain.Entities
{
    /// <summary>
    /// Sessiya uchun bitta to'lov niyati — uchala strategiya (Merchant/Invoice/Subscribe) shu
    /// yozuvdan foydalanadi. Status faqat <c>PaymentIntentStateMachine</c> ruxsat jadvali bo'yicha,
    /// <c>IPaymentIntentRepository.TryTransitionAsync</c> orqali o'zgaradi.
    /// Watcher (SessionApi) barcha provider settlement chaqiruvlarini bajaradi;
    /// boshqa oqimlar faqat maqsad holat (SettlePending/RefundPending) qo'yadi.
    /// Barcha summalar integer TIYIN.
    /// </summary>
    public class PaymentIntentEntity : Entity
    {
        public long PaymentSessionId { get; set; }
        public PaymentSessionEntity? PaymentSession { get; set; }

        /// <summary>Qaysi strategiya yaratgan. PaymentSession.Method bilan bir xil bo'ladi.</summary>
        public PaymentMethod Method { get; set; } = PaymentMethod.Subscribe;

        /// <summary>Pul ushlanadimi (Hold) yoki darhol yechiladimi (Charge).</summary>
        public PaymentIntentKind Kind { get; set; } = PaymentIntentKind.Hold;

        /// <summary>FIFO tartib — sessiya ichida 1 dan boshlab o'sadi. N to'liq tugamaguncha N+1 ga tegilmaydi.</summary>
        public int SequenceNo { get; set; }

        /// <summary>Ta'minlanadigan summa (tiyin).</summary>
        public long AmountTiyin { get; set; }

        /// <summary>Dispense'larga ishlatilgan qism (tiyin). Amount'dan oshmaydi.</summary>
        public long ConsumedTiyin { get; set; }

        /// <summary>SettlePending'da yechiladigan summa (tiyin) — odatda ConsumedTiyin.
        /// Hold'da Payme confirm_hold(amount) qisman yechadi, qolgani avtomatik bo'shaydi.</summary>
        public long? CaptureAmountTiyin { get; set; }

        public PaymentIntentStatus Status { get; set; } = PaymentIntentStatus.Created;

        /// <summary>Payme receipt _id (Receipts/Subscribe API).</summary>
        public string? ProviderReceiptId { get; set; }

        /// <summary>Bizning order_id — providerga yuboriladi. Unique.</summary>
        public string ProviderOrderId { get; set; } = string.Empty;

        /// <summary>Payme receipt state raqami (oxirgi ko'rilgan).</summary>
        public int? ProviderState { get; set; }

        // ── Merchant API (provider bizga callback qiladi) ────────────
        // Payme Merchant API o'z tranzaksiya identifikatorini va vaqtlarini talab qiladi;
        // alohida jadval ochmaymiz — FIFO/settlement mantiqi bitta jadvalda qolsin.

        /// <summary>Payme tomondagi tranzaksiya id (Merchant API).</summary>
        public string? ProviderTransactionId { get; set; }

        /// <summary>Payme yuborgan tranzaksiya vaqti (ms, unix) — CheckTransaction javobida qaytariladi.</summary>
        public long? ProviderTransactionTime { get; set; }

        public DateTime? ProviderCreatedAt { get; set; }
        public DateTime? ProviderPerformedAt { get; set; }
        public DateTime? ProviderCancelledAt { get; set; }

        /// <summary>Payme bekor qilish sababi kodi (Merchant API reason).</summary>
        public int? ProviderCancelReason { get; set; }

        /// <summary>Subscribe: qaysi saqlangan karta bilan to'landi.</summary>
        public long? CustomerCardId { get; set; }
        public CustomerCardEntity? CustomerCard { get; set; }

        /// <summary>Mijozga ko'rsatiladigan to'lov havolasi (Merchant checkout / Invoice deep-link).</summary>
        public string? CheckoutUrl { get; set; }

        public string? IdempotencyKey { get; set; }

        public long CreatedByUserId { get; set; }

        public string? FailureReason { get; set; }

        // ── Watcher retry/lease maydonlari ──────────────────────────
        public int AttemptCount { get; set; }

        /// <summary>Watcher keyingi urinish vaqti. NULL — navbatda emas.</summary>
        public DateTime? NextAttemptAt { get; set; }

        /// <summary>Lease egasi (instance id) — parallel tick/instance'lar bir intent'ni ikki marta olmasligi uchun.</summary>
        public string? LockedBy { get; set; }

        public DateTime? LeaseUntil { get; set; }

        /// <summary>Mablag' ta'minlangan payt (hold ushlandi yoki to'lov o'tdi).</summary>
        public DateTime? FundedAt { get; set; }

        public DateTime? SettledAt { get; set; }

        /// <summary>Optimistic concurrency (PostgreSQL xmin).</summary>
        public uint RowVersion { get; set; }

        public ICollection<PaymentIntentStepEntity>? Steps { get; set; }
    }
}
