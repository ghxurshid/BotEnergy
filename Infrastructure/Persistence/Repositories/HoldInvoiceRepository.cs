using Domain.Entities;
using Domain.Enums;
using Domain.Helpers;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories
{
    public class HoldInvoiceRepository : IHoldInvoiceRepository
    {
        private static readonly HoldInvoiceStatus[] WatcherStatuses =
        {
            HoldInvoiceStatus.WaitingForConfirmation,
            HoldInvoiceStatus.CapturePending,
            HoldInvoiceStatus.RefundPending
        };

        private readonly AppDbContext _context;

        public HoldInvoiceRepository(AppDbContext context)
            => _context = context;

        public async Task<HoldInvoiceEntity> CreateAsync(HoldInvoiceEntity invoice)
        {
            await _context.HoldInvoices.AddAsync(invoice);
            await _context.SaveChangesAsync();
            return invoice;
        }

        public Task<HoldInvoiceEntity?> GetByIdAsync(long id, bool includeSteps = false)
        {
            var query = _context.HoldInvoices.Include(i => i.PaymentSession).AsQueryable();
            if (includeSteps)
                query = query.Include(i => i.Steps!.OrderBy(s => s.OccurredAt));
            return query.FirstOrDefaultAsync(i => i.Id == id);
        }

        public Task<HoldInvoiceEntity?> GetByIdempotencyKeyAsync(string idempotencyKey)
            => _context.HoldInvoices.FirstOrDefaultAsync(i => i.IdempotencyKey == idempotencyKey);

        public Task<List<HoldInvoiceEntity>> GetByPaymentSessionAsync(long paymentSessionId)
            => _context.HoldInvoices
                .Where(i => i.PaymentSessionId == paymentSessionId)
                .OrderBy(i => i.SequenceNo)
                .ToListAsync();

        public Task<int> CountActiveForPaymentSessionAsync(long paymentSessionId)
            => _context.HoldInvoices
                .CountAsync(i => i.PaymentSessionId == paymentSessionId
                              && i.Status != HoldInvoiceStatus.Captured
                              && i.Status != HoldInvoiceStatus.Refunded
                              && i.Status != HoldInvoiceStatus.Cancelled
                              && i.Status != HoldInvoiceStatus.Expired
                              && i.Status != HoldInvoiceStatus.Failed);

        public async Task<int> NextSequenceNoAsync(long paymentSessionId)
        {
            var max = await _context.HoldInvoices
                .Where(i => i.PaymentSessionId == paymentSessionId)
                .MaxAsync(i => (int?)i.SequenceNo);
            return (max ?? 0) + 1;
        }

        public async Task<bool> TryTransitionAsync(long id, HoldInvoiceStatus to,
            long? captureAmountTiyin = null,
            DateTime? nextAttemptAt = null,
            string? failureReason = null)
        {
            var allowedFrom = HoldInvoiceStateMachine.SourcesFor(to);
            if (allowedFrom.Length == 0)
                return false;

            var now = DateTime.Now;
            var isTerminal = HoldInvoiceStateMachine.IsTerminal(to);

            var affected = await _context.HoldInvoices
                .Where(i => i.Id == id && allowedFrom.Contains(i.Status))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.Status, to)
                    .SetProperty(i => i.CaptureAmountTiyin, i => captureAmountTiyin ?? i.CaptureAmountTiyin)
                    .SetProperty(i => i.NextAttemptAt, nextAttemptAt)
                    .SetProperty(i => i.FailureReason, i => failureReason ?? i.FailureReason)
                    .SetProperty(i => i.HoldAt, i => to == HoldInvoiceStatus.Hold ? now : i.HoldAt)
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
UPDATE app.hold_invoices AS h
SET consumed_tiyin = h.consumed_tiyin + LEAST({wantedTiyin}, h.amount_tiyin - h.consumed_tiyin),
    updated_date   = LOCALTIMESTAMP
FROM (SELECT id, consumed_tiyin AS old_consumed
      FROM app.hold_invoices
      WHERE id = {id}
      FOR UPDATE) AS o
WHERE h.id = o.id
  AND h.status IN ({(int)HoldInvoiceStatus.Hold}, {(int)HoldInvoiceStatus.PartiallyConsumed})
  AND h.consumed_tiyin < h.amount_tiyin
RETURNING h.consumed_tiyin - o.old_consumed AS ""Value""")
                .ToListAsync();

            return applied.Count > 0 ? applied[0] : 0;
        }

        public async Task<List<HoldInvoiceEntity>> ClaimDueAsync(string ownerId, DateTime leaseUntil, int batch)
        {
            var statuses = WatcherStatuses.Select(s => (int)s).ToArray();

            // Raw SQL parametrida DateTime Kind=Local'ni Npgsql 'timestamptz' deb hisoblab rad etadi
            // (mapped ustundan farqli — bu yerda ustun turi haqida ma'lumot yo'q). Mahalliy vaqt
            // konvensiyamizga mos ravishda Unspecified qilamiz → 'timestamp without time zone'ga
            // to'g'ri yoziladi. "Hozir" uchun esa server soatini (LOCALTIMESTAMP) ishlatamiz.
            var lease = DateTime.SpecifyKind(leaseUntil, DateTimeKind.Unspecified);

            // SKIP LOCKED — parallel tick/instance'lar bir-birini kutmaydi va bir invoice'ni ikki marta olmaydi.
            var claimedIds = await _context.Database
                .SqlQuery<long>($@"
UPDATE app.hold_invoices
SET locked_by = {ownerId}, lease_until = {lease}, updated_date = LOCALTIMESTAMP
WHERE id IN (
    SELECT id FROM app.hold_invoices
    WHERE status = ANY({statuses})
      AND next_attempt_at IS NOT NULL AND next_attempt_at <= LOCALTIMESTAMP
      AND (lease_until IS NULL OR lease_until < LOCALTIMESTAMP)
      AND is_deleted = false
    ORDER BY next_attempt_at
    LIMIT {batch}
    FOR UPDATE SKIP LOCKED)
RETURNING id AS ""Value""")
                .ToListAsync();

            if (claimedIds.Count == 0)
                return new List<HoldInvoiceEntity>();

            return await _context.HoldInvoices
                .Where(i => claimedIds.Contains(i.Id))
                .OrderBy(i => i.NextAttemptAt)
                .ToListAsync();
        }

        public Task ReleaseLeaseAsync(long id, string ownerId)
            => _context.HoldInvoices
                .Where(i => i.Id == id && i.LockedBy == ownerId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.LockedBy, (string?)null)
                    .SetProperty(i => i.LeaseUntil, (DateTime?)null));

        public Task ScheduleRetryAsync(long id, DateTime nextAttemptAt, string? failureReason)
        {
            var now = DateTime.Now;
            return _context.HoldInvoices
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
            return _context.HoldInvoices
                .Where(i => i.Id == id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.NextAttemptAt, nextAttemptAt)
                    .SetProperty(i => i.ProviderState, i => providerState ?? i.ProviderState)
                    .SetProperty(i => i.LockedBy, (string?)null)
                    .SetProperty(i => i.LeaseUntil, (DateTime?)null)
                    .SetProperty(i => i.UpdatedDate, now));
        }

        public Task<bool> AnyNonTerminalAsync(long paymentSessionId)
            => _context.HoldInvoices
                .AnyAsync(i => i.PaymentSessionId == paymentSessionId
                            && i.Status != HoldInvoiceStatus.Captured
                            && i.Status != HoldInvoiceStatus.Refunded
                            && i.Status != HoldInvoiceStatus.Cancelled
                            && i.Status != HoldInvoiceStatus.Expired);

        public async Task UpdateAsync(HoldInvoiceEntity invoice)
        {
            if (_context.Entry(invoice).State == EntityState.Detached)
                _context.HoldInvoices.Update(invoice);
            await _context.SaveChangesAsync();
        }

        public async Task AddStepAsync(HoldInvoiceStepEntity step)
        {
            await _context.HoldInvoiceSteps.AddAsync(step);
            await _context.SaveChangesAsync();
        }

        public Task<List<HoldInvoiceStepEntity>> GetStepsAsync(long invoiceId)
            => _context.HoldInvoiceSteps
                .Where(s => s.HoldInvoiceId == invoiceId)
                .OrderBy(s => s.OccurredAt)
                .ToListAsync();

        public Task<List<HoldInvoiceEntity>> ListAllAsync(
            int skip, int take,
            long? merchantId = null,
            long? sessionId = null,
            HoldInvoiceStatus? status = null,
            DateTime? from = null,
            DateTime? to = null)
        {
            var query = _context.HoldInvoices
                .Include(i => i.PaymentSession)
                .AsQueryable();

            if (merchantId.HasValue) query = query.Where(i => i.PaymentSession!.MerchantId == merchantId);
            if (sessionId.HasValue) query = query.Where(i => i.PaymentSession!.SessionId == sessionId);
            if (status.HasValue) query = query.Where(i => i.Status == status);
            if (from.HasValue) query = query.Where(i => i.CreatedDate >= from);
            if (to.HasValue) query = query.Where(i => i.CreatedDate <= to);

            return query
                .OrderByDescending(i => i.CreatedDate)
                .Skip(skip).Take(take)
                .ToListAsync();
        }
    }
}
