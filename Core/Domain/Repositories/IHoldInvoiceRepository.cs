using Domain.Entities;
using Domain.Enums;

namespace Domain.Repositories
{
    public interface IHoldInvoiceRepository
    {
        Task<HoldInvoiceEntity> CreateAsync(HoldInvoiceEntity invoice);

        Task<HoldInvoiceEntity?> GetByIdAsync(long id, bool includeSteps = false);

        Task<HoldInvoiceEntity?> GetByIdempotencyKeyAsync(string idempotencyKey);

        /// <summary>Sessiya invoice'lari FIFO tartibda (SequenceNo ASC).</summary>
        Task<List<HoldInvoiceEntity>> GetByPaymentSessionAsync(long paymentSessionId);

        /// <summary>Terminal bo'lmagan (aktiv) invoice'lar soni — MaxInvoicesPerSession limiti uchun.</summary>
        Task<int> CountActiveForPaymentSessionAsync(long paymentSessionId);

        /// <summary>Keyingi FIFO tartib raqami (max(SequenceNo)+1).</summary>
        Task<int> NextSequenceNoAsync(long paymentSessionId);

        /// <summary>
        /// YAGONA status yozish nuqtasi. HoldInvoiceStateMachine ruxsatini tekshiradi,
        /// atomik ExecuteUpdate (WHERE status IN allowedFrom) bilan yozadi.
        /// False — joriy status ruxsat bermadi (poyga yoki noto'g'ri o'tish).
        /// </summary>
        Task<bool> TryTransitionAsync(long id, HoldInvoiceStatus to,
            long? captureAmountTiyin = null,
            DateTime? nextAttemptAt = null,
            string? failureReason = null);

        /// <summary>
        /// FIFO-slice consume: bitta SQL'da consumed_tiyin += LEAST(wanted, qolgan) — faqat
        /// Hold/PartiallyConsumed holatlarda. Qo'llangan real delta (tiyin) qaytadi (0 — hech narsa).
        /// </summary>
        Task<long> ConsumeAtomicAsync(long id, long wantedTiyin);

        /// <summary>
        /// Watcher uchun navbatdagi invoice'larni lease bilan claim qiladi:
        /// status IN (WaitingForConfirmation, CapturePending, RefundPending)
        /// AND next_attempt_at &lt;= now AND (lease yo'q yoki muddati o'tgan).
        /// </summary>
        Task<List<HoldInvoiceEntity>> ClaimDueAsync(string ownerId, DateTime leaseUntil, int batch);

        Task ReleaseLeaseAsync(long id, string ownerId);

        /// <summary>Transient xatoda keyingi urinishni rejalashtiradi (AttemptCount++).</summary>
        Task ScheduleRetryAsync(long id, DateTime nextAttemptAt, string? failureReason);

        /// <summary>
        /// Oddiy polling davomi (WaitingForConfirmation) — AttemptCount O'SMAYDI,
        /// chunki bu xato emas, mijoz to'lovini kutish.
        /// </summary>
        Task SchedulePollAsync(long id, DateTime nextAttemptAt, int? providerState = null);

        /// <summary>Payment session'da terminal bo'lmagan invoice qolganmi (Settled shartini tekshirish).</summary>
        Task<bool> AnyNonTerminalAsync(long paymentSessionId);

        Task UpdateAsync(HoldInvoiceEntity invoice);

        /// <summary>Append-only audit qadam.</summary>
        Task AddStepAsync(HoldInvoiceStepEntity step);

        Task<List<HoldInvoiceStepEntity>> GetStepsAsync(long invoiceId);

        /// <summary>Admin audit ro'yxati — filter + paginatsiya.</summary>
        Task<List<HoldInvoiceEntity>> ListAllAsync(
            int skip, int take,
            long? merchantId = null,
            long? sessionId = null,
            HoldInvoiceStatus? status = null,
            DateTime? from = null,
            DateTime? to = null);
    }
}
