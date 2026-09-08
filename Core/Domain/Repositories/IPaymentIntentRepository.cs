using Domain.Entities;
using Domain.Enums;

namespace Domain.Repositories
{
    public interface IPaymentIntentRepository
    {
        Task<PaymentIntentEntity> CreateAsync(PaymentIntentEntity invoice);

        Task<PaymentIntentEntity?> GetByIdAsync(long id, bool includeSteps = false);

        Task<PaymentIntentEntity?> GetByIdempotencyKeyAsync(string idempotencyKey);

        /// <summary>Provider bizga callback qilganda intent'ni order_id bo'yicha topish (Merchant API).</summary>
        Task<PaymentIntentEntity?> GetByProviderOrderIdAsync(string providerOrderId);

        /// <summary>Provider tranzaksiya id bo'yicha topish (Merchant API: Perform/Cancel/Check).</summary>
        Task<PaymentIntentEntity?> GetByProviderTransactionIdAsync(string providerTransactionId);

        /// <summary>GetStatement: merchantning berilgan oraliqdagi provider tranzaksiyalari.</summary>
        Task<List<PaymentIntentEntity>> GetForMerchantRangeAsync(
            long merchantId, PaymentMethod method, DateTime from, DateTime to);

        /// <summary>Sessiya intent'lari FIFO tartibda (SequenceNo ASC).</summary>
        Task<List<PaymentIntentEntity>> GetByPaymentSessionAsync(long paymentSessionId);

        /// <summary>Terminal bo'lmagan (aktiv) intent'lar soni — MaxIntentsPerSession limiti uchun.</summary>
        Task<int> CountActiveForPaymentSessionAsync(long paymentSessionId);

        /// <summary>Keyingi FIFO tartib raqami (max(SequenceNo)+1).</summary>
        Task<int> NextSequenceNoAsync(long paymentSessionId);

        /// <summary>
        /// YAGONA status yozish nuqtasi. PaymentIntentStateMachine ruxsatini tekshiradi,
        /// atomik ExecuteUpdate (WHERE status IN allowedFrom) bilan yozadi.
        /// False — joriy status ruxsat bermadi (poyga yoki noto'g'ri o'tish).
        /// </summary>
        Task<bool> TryTransitionAsync(long id, PaymentIntentStatus to,
            long? captureAmountTiyin = null,
            DateTime? nextAttemptAt = null,
            string? failureReason = null);

        /// <summary>
        /// FIFO-slice consume: bitta SQL'da consumed_tiyin += LEAST(wanted, qolgan) — faqat
        /// Hold/PartiallyConsumed holatlarda. Qo'llangan real delta (tiyin) qaytadi (0 — hech narsa).
        /// </summary>
        Task<long> ConsumeAtomicAsync(long id, long wantedTiyin);

        /// <summary>
        /// Watcher uchun navbatdagi intent'larni lease bilan claim qiladi:
        /// method = <paramref name="method"/> AND status IN (WaitingForConfirmation, SettlePending,
        /// RefundPending) AND next_attempt_at &lt;= now AND (lease yo'q yoki muddati o'tgan).
        /// Usul bo'yicha filtr MAJBURIY — har strategiya faqat o'zi yaratgan intent'larni ishlaydi.
        /// </summary>
        Task<List<PaymentIntentEntity>> ClaimDueAsync(
            PaymentMethod method, string ownerId, DateTime leaseUntil, int batch);

        Task ReleaseLeaseAsync(long id, string ownerId);

        /// <summary>Transient xatoda keyingi urinishni rejalashtiradi (AttemptCount++).</summary>
        Task ScheduleRetryAsync(long id, DateTime nextAttemptAt, string? failureReason);

        /// <summary>
        /// Oddiy polling davomi (WaitingForConfirmation) — AttemptCount O'SMAYDI,
        /// chunki bu xato emas, mijoz to'lovini kutish.
        /// </summary>
        Task SchedulePollAsync(long id, DateTime nextAttemptAt, int? providerState = null);

        /// <summary>Payment session'da terminal bo'lmagan intent qolganmi (Settled shartini tekshirish).</summary>
        Task<bool> AnyNonTerminalAsync(long paymentSessionId);

        /// <summary>
        /// Failed'dan BOSHQA terminal bo'lmagan intent qolganmi. Failed operator aralashuvini
        /// kutadi va o'z-o'zidan hech qachon yechilmaydi — shuning uchun u qurilma sessiyasini
        /// abadiy ushlab turmasligi kerak (moliyaviy yakun esa ochiq qoladi).
        /// </summary>
        Task<bool> AnyNonTerminalExceptFailedAsync(long paymentSessionId);

        Task UpdateAsync(PaymentIntentEntity invoice);

        /// <summary>Append-only audit qadam.</summary>
        Task AddStepAsync(PaymentIntentStepEntity step);

        Task<List<PaymentIntentStepEntity>> GetStepsAsync(long invoiceId);

        /// <summary>Admin audit ro'yxati — filter + paginatsiya.</summary>
        Task<List<PaymentIntentEntity>> ListAllAsync(
            int skip, int take,
            long? merchantId = null,
            long? sessionId = null,
            PaymentIntentStatus? status = null,
            PaymentMethod? method = null,
            DateTime? from = null,
            DateTime? to = null);
    }
}
