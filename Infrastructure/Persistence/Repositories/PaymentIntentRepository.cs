using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Helpers;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Persistence.Context;

namespace Persistence.Repositories
{
    public class PaymentIntentRepository : IPaymentIntentRepository
    {
        private static readonly PaymentIntentStatus[] WatcherStatuses =
        {
            PaymentIntentStatus.WaitingForConfirmation,
            PaymentIntentStatus.SettlePending,
            PaymentIntentStatus.RefundPending
        };

        private readonly AppDbContext _context;

        public PaymentIntentRepository(AppDbContext context)
            => _context = context;

        /// <summary>
        /// SequenceNo (payment_session_id, sequence_no) unique — ikki parallel so'rov bir xil
        /// raqamni olib qolishi mumkin. Bunda 500 qaytarish o'rniga raqamni qayta hisoblab
        /// urinib ko'ramiz (mobil ilovada ikki marta bosish real ssenariy).
        /// </summary>
        public async Task<PaymentIntentEntity> CreateAsync(PaymentIntentEntity invoice)
        {
            const int maxAttempts = 3;

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await _context.PaymentIntents.AddAsync(invoice);
                    await _context.SaveChangesAsync();
                    return invoice;
                }
                catch (DbUpdateException ex) when (attempt < maxAttempts && IsUniqueViolationOn(ex, "sequence_no"))
                {
                    // Yozuvni tracker'dan chiqaramiz, aks holda keyingi SaveChanges o'shani takrorlaydi.
                    _context.Entry(invoice).State = EntityState.Detached;
                    invoice.SequenceNo = await NextSequenceNoAsync(invoice.PaymentSessionId);
                }
                catch (DbUpdateException ex) when (IsUniqueViolationOn(ex, "idempotency_key")
                                                  && !string.IsNullOrEmpty(invoice.IdempotencyKey))
                {
                    // Ikki bir xil so'rov bir vaqtda yetib keldi. Bu XATO emas — takroriy so'rov;
                    // chaqiruvchi mavjud to'lovni qaytarishi kerak (provider qayta chaqirilmaydi).
                    _context.Entry(invoice).State = EntityState.Detached;
                    throw new DuplicateIdempotencyKeyException(invoice.IdempotencyKey!);
                }
            }
        }

        /// <summary>Unique cheklov aynan berilgan ustun bo'yicha buzildimi.</summary>
        private static bool IsUniqueViolationOn(DbUpdateException ex, string columnFragment)
            => ex.InnerException is PostgresException { SqlState: "23505" } pg
               && (pg.ConstraintName?.Contains(columnFragment, StringComparison.OrdinalIgnoreCase) ?? false);

        public Task<PaymentIntentEntity?> GetByIdAsync(long id, bool includeSteps = false)
        {
            var query = _context.PaymentIntents.Include(i => i.PaymentSession).AsQueryable();
            if (includeSteps)
                query = query.Include(i => i.Steps!.OrderBy(s => s.OccurredAt));
            return query.FirstOrDefaultAsync(i => i.Id == id);
        }

        public Task<PaymentIntentEntity?> GetByIdempotencyKeyAsync(string idempotencyKey)
            => _context.PaymentIntents.FirstOrDefaultAsync(i => i.IdempotencyKey == idempotencyKey);

        public Task<PaymentIntentEntity?> GetByProviderOrderIdAsync(string providerOrderId)
            => _context.PaymentIntents
                .Include(i => i.PaymentSession)
                .FirstOrDefaultAsync(i => i.ProviderOrderId == providerOrderId);

        public Task<PaymentIntentEntity?> GetByProviderTransactionIdAsync(string providerTransactionId)
            => _context.PaymentIntents
                .Include(i => i.PaymentSession)
                .FirstOrDefaultAsync(i => i.ProviderTransactionId == providerTransactionId);

        public Task<List<PaymentIntentEntity>> GetForMerchantRangeAsync(
            long merchantId, PaymentMethod method, DateTime from, DateTime to)
            => _context.PaymentIntents
                .Include(i => i.PaymentSession)
                .Where(i => i.Method == method
                         && i.PaymentSession!.MerchantId == merchantId
                         && i.ProviderTransactionId != null
                         && i.ProviderCreatedAt >= from
                         && i.ProviderCreatedAt <= to)
                .OrderBy(i => i.ProviderCreatedAt)
                .ToListAsync();

        public Task<List<PaymentIntentEntity>> GetByPaymentSessionAsync(long paymentSessionId)
            => _context.PaymentIntents
                .Where(i => i.PaymentSessionId == paymentSessionId)
                .OrderBy(i => i.SequenceNo)
                .ToListAsync();

        public Task<int> CountActiveForPaymentSessionAsync(long paymentSessionId)
            => _context.PaymentIntents
                .CountAsync(i => i.PaymentSessionId == paymentSessionId
                              && i.Status != PaymentIntentStatus.Settled
                              && i.Status != PaymentIntentStatus.Refunded
                              && i.Status != PaymentIntentStatus.Cancelled
                              && i.Status != PaymentIntentStatus.Expired
                              && i.Status != PaymentIntentStatus.Failed);

        public async Task<int> NextSequenceNoAsync(long paymentSessionId)
        {
            var max = await _context.PaymentIntents
                .Where(i => i.PaymentSessionId == paymentSessionId)
                .MaxAsync(i => (int?)i.SequenceNo);
            return (max ?? 0) + 1;
        }

        public async Task<bool> TryTransitionAsync(long id, PaymentIntentStatus to,
            long? captureAmountTiyin = null,
            DateTime? nextAttemptAt = null,
            string? failureReason = null)
        {
            var allowedFrom = PaymentIntentStateMachine.SourcesFor(to);
            if (allowedFrom.Length == 0)
                return false;

            var now = DateTime.Now;
            var isTerminal = PaymentIntentStateMachine.IsTerminal(to);

            var affected = await _context.PaymentIntents
                .Where(i => i.Id == id && allowedFrom.Contains(i.Status))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.Status, to)
                    .SetProperty(i => i.CaptureAmountTiyin, i => captureAmountTiyin ?? i.CaptureAmountTiyin)
                    .SetProperty(i => i.NextAttemptAt, nextAttemptAt)
                    .SetProperty(i => i.FailureReason, i => failureReason ?? i.FailureReason)
                    .SetProperty(i => i.FundedAt, i => to == PaymentIntentStatus.Funded ? now : i.FundedAt)
                    .SetProperty(i => i.SettledAt, i => isTerminal ? now : i.SettledAt)
                    .SetProperty(i => i.LockedBy, (string?)null)
                    .SetProperty(i => i.LeaseUntil, (DateTime?)null)
                    .SetProperty(i => i.UpdatedDate, now));

            return affected > 0;
        }

        public async Task<long> ConsumeAtomicAsync(long id, long wantedTiyin)
        {
            if (wantedTiyin <= 0) return 0;

            // FIFO-slice: consumed += LEAST(wanted, qolgan) — bitta statement, poyga xavfsiz.
            // Faqat Hold(2)/PartiallyConsumed(3) holatlarda; RETURNING orqali real qo'llangan delta.
            var applied = await _context.Database
                .SqlQuery<long>($@"
UPDATE app.payment_intents AS h
SET consumed_tiyin = h.consumed_tiyin + LEAST({wantedTiyin}, h.amount_tiyin - h.consumed_tiyin),
    updated_date   = LOCALTIMESTAMP
FROM (SELECT id, consumed_tiyin AS old_consumed
      FROM app.payment_intents
      WHERE id = {id}
      FOR UPDATE) AS o
WHERE h.id = o.id
  AND h.status IN ({(int)PaymentIntentStatus.Funded}, {(int)PaymentIntentStatus.PartiallyConsumed})
  AND h.consumed_tiyin < h.amount_tiyin
RETURNING h.consumed_tiyin - o.old_consumed AS ""Value""")
                .ToListAsync();

            return applied.Count > 0 ? applied[0] : 0;
        }

        public async Task<List<PaymentIntentEntity>> ClaimDueAsync(
            PaymentMethod method, string ownerId, DateTime leaseUntil, int batch)
        {
            var statuses = WatcherStatuses.Select(s => (int)s).ToArray();

            // Raw SQL da DateTime parametrining Npgsql standart PG turi 'timestamptz' — u faqat UTC
            // qabul qiladi, shuning uchun mahalliy vaqt (Local/Unspecified) rad etiladi (mapped
            // ustundan farqli — bu yerda ustun turi haqida ma'lumot yo'q). Turni oshkora
            // 'timestamp without time zone' qilib beramiz. "Hozir" uchun server soati LOCALTIMESTAMP.
            var leaseParam = new NpgsqlParameter("lease", NpgsqlDbType.Timestamp)
            {
                Value = DateTime.SpecifyKind(leaseUntil, DateTimeKind.Unspecified)
            };

            // SKIP LOCKED — parallel tick/instance'lar bir-birini kutmaydi va bir invoice'ni ikki marta olmaydi.
            var claimedIds = await _context.Database
                .SqlQuery<long>($@"
UPDATE app.payment_intents
SET locked_by = {ownerId}, lease_until = {leaseParam}, updated_date = LOCALTIMESTAMP
WHERE id IN (
    SELECT id FROM app.payment_intents
    WHERE method = {(int)method}
      AND status = ANY({statuses})
      AND next_attempt_at IS NOT NULL AND next_attempt_at <= LOCALTIMESTAMP
      AND (lease_until IS NULL OR lease_until < LOCALTIMESTAMP)
      AND is_deleted = false
    ORDER BY next_attempt_at
    LIMIT {batch}
    FOR UPDATE SKIP LOCKED)
RETURNING id AS ""Value""")
                .ToListAsync();

            if (claimedIds.Count == 0)
                return new List<PaymentIntentEntity>();

            return await _context.PaymentIntents
                .Where(i => claimedIds.Contains(i.Id))
                .OrderBy(i => i.NextAttemptAt)
                .ToListAsync();
        }

        public Task ReleaseLeaseAsync(long id, string ownerId)
            => _context.PaymentIntents
                .Where(i => i.Id == id && i.LockedBy == ownerId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.LockedBy, (string?)null)
                    .SetProperty(i => i.LeaseUntil, (DateTime?)null));

        public Task ScheduleRetryAsync(long id, DateTime nextAttemptAt, string? failureReason)
        {
            var now = DateTime.Now;
            return _context.PaymentIntents
                .Where(i => i.Id == id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.AttemptCount, i => i.AttemptCount + 1)
                    .SetProperty(i => i.NextAttemptAt, nextAttemptAt)
                    .SetProperty(i => i.FailureReason, i => failureReason ?? i.FailureReason)
                    .SetProperty(i => i.LockedBy, (string?)null)
                    .SetProperty(i => i.LeaseUntil, (DateTime?)null)
                    .SetProperty(i => i.UpdatedDate, now));
        }

        public Task SchedulePollAsync(long id, DateTime nextAttemptAt, int? providerState = null)
        {
            var now = DateTime.Now;
            return _context.PaymentIntents
                .Where(i => i.Id == id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.NextAttemptAt, nextAttemptAt)
                    .SetProperty(i => i.ProviderState, i => providerState ?? i.ProviderState)
                    .SetProperty(i => i.LockedBy, (string?)null)
                    .SetProperty(i => i.LeaseUntil, (DateTime?)null)
                    .SetProperty(i => i.UpdatedDate, now));
        }

        public Task<bool> AnyNonTerminalExceptFailedAsync(long paymentSessionId)
            => _context.PaymentIntents
                .AnyAsync(i => i.PaymentSessionId == paymentSessionId
                            && i.Status != PaymentIntentStatus.Settled
                            && i.Status != PaymentIntentStatus.Refunded
                            && i.Status != PaymentIntentStatus.Cancelled
                            && i.Status != PaymentIntentStatus.Expired
                            && i.Status != PaymentIntentStatus.Failed);

        public Task<bool> AnyNonTerminalAsync(long paymentSessionId)
            => _context.PaymentIntents
                .AnyAsync(i => i.PaymentSessionId == paymentSessionId
                            && i.Status != PaymentIntentStatus.Settled
                            && i.Status != PaymentIntentStatus.Refunded
                            && i.Status != PaymentIntentStatus.Cancelled
                            && i.Status != PaymentIntentStatus.Expired);

        public async Task UpdateAsync(PaymentIntentEntity invoice)
        {
            if (_context.Entry(invoice).State == EntityState.Detached)
                _context.PaymentIntents.Update(invoice);
            await _context.SaveChangesAsync();
        }

        public async Task AddStepAsync(PaymentIntentStepEntity step)
        {
            await _context.PaymentIntentSteps.AddAsync(step);
            await _context.SaveChangesAsync();
        }

        public Task<List<PaymentIntentStepEntity>> GetStepsAsync(long invoiceId)
            => _context.PaymentIntentSteps
                .Where(s => s.PaymentIntentId == invoiceId)
                .OrderBy(s => s.OccurredAt)
                .ToListAsync();

        public Task<List<PaymentIntentEntity>> ListAllAsync(
            int skip, int take,
            long? merchantId = null,
            long? sessionId = null,
            PaymentIntentStatus? status = null,
            PaymentMethod? method = null,
            DateTime? from = null,
            DateTime? to = null)
        {
            var query = _context.PaymentIntents
                .Include(i => i.PaymentSession)
                .AsQueryable();

            if (merchantId.HasValue) query = query.Where(i => i.PaymentSession!.MerchantId == merchantId);
            if (sessionId.HasValue) query = query.Where(i => i.PaymentSession!.SessionId == sessionId);
            if (status.HasValue) query = query.Where(i => i.Status == status);
            if (method.HasValue) query = query.Where(i => i.Method == method);
            if (from.HasValue) query = query.Where(i => i.CreatedDate >= from);
            if (to.HasValue) query = query.Where(i => i.CreatedDate <= to);

            return query
                .OrderByDescending(i => i.CreatedDate)
                .Skip(skip).Take(take)
                .ToListAsync();
        }
    }
}
