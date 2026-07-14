using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories
{
    public class PaymentSessionRepository : IPaymentSessionRepository
    {
        private readonly AppDbContext _context;

        public PaymentSessionRepository(AppDbContext context)
            => _context = context;

        public async Task<PaymentSessionEntity> CreateAsync(PaymentSessionEntity paymentSession)
        {
            await _context.PaymentSessions.AddAsync(paymentSession);
            await _context.SaveChangesAsync();
            return paymentSession;
        }

        public Task<PaymentSessionEntity?> GetByIdAsync(long id)
            => _context.PaymentSessions.FirstOrDefaultAsync(p => p.Id == id);

        public Task<PaymentSessionEntity?> GetBySessionIdAsync(long sessionId, bool includeInvoices = false)
        {
            var query = _context.PaymentSessions.AsQueryable();
            if (includeInvoices)
                query = query.Include(p => p.Invoices!.OrderBy(i => i.SequenceNo));
            return query.FirstOrDefaultAsync(p => p.SessionId == sessionId);
        }

        public async Task<bool> TryTransitionAsync(long id, PaymentSessionStatus to, params PaymentSessionStatus[] from)
        {
            var now = DateTime.Now;
            var affected = await _context.PaymentSessions
                .Where(p => p.Id == id && from.Contains(p.Status))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.Status, to)
                    .SetProperty(p => p.SettledAt, p => to == PaymentSessionStatus.Settled ? now : p.SettledAt)
                    .SetProperty(p => p.UpdatedDate, now));
            return affected > 0;
        }

        public Task TryAddHoldBalanceAsync(long id, long deltaTiyin)
        {
            var now = DateTime.Now;
            return _context.PaymentSessions
                .Where(p => p.Id == id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.HoldBalanceTiyin, p => p.HoldBalanceTiyin + deltaTiyin)
                    .SetProperty(p => p.UpdatedDate, now));
        }

        public async Task<bool> TryConsumeBalanceAsync(long id, long deltaTiyin)
        {
            var now = DateTime.Now;
            var affected = await _context.PaymentSessions
                .Where(p => p.Id == id && p.HoldBalanceTiyin - p.ConsumedTiyin >= deltaTiyin)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.ConsumedTiyin, p => p.ConsumedTiyin + deltaTiyin)
                    .SetProperty(p => p.UpdatedDate, now));
            return affected > 0;
        }

        public Task<List<PaymentSessionEntity>> GetSettlingAsync(int take)
            => _context.PaymentSessions
                .Where(p => p.Status == PaymentSessionStatus.Settling)
                .OrderBy(p => p.UpdatedDate)
                .Take(take)
                .ToListAsync();

        public async Task UpdateAsync(PaymentSessionEntity paymentSession)
        {
            if (_context.Entry(paymentSession).State == EntityState.Detached)
                _context.PaymentSessions.Update(paymentSession);
            await _context.SaveChangesAsync();
        }
    }
}
