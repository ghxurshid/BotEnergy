using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories
{
    public class PaymentTransactionRepository : IPaymentTransactionRepository
    {
        private readonly AppDbContext _context;

        public PaymentTransactionRepository(AppDbContext context)
            => _context = context;

        public async Task<PaymentTransactionEntity> CreateAsync(PaymentTransactionEntity transaction)
        {
            await _context.PaymentTransactions.AddAsync(transaction);
            await _context.SaveChangesAsync();
            return transaction;
        }

        public async Task<PaymentTransactionEntity?> GetByIdAsync(long id, bool includeSteps = false)
        {
            var query = _context.PaymentTransactions.AsQueryable();
            if (includeSteps)
                query = query.Include(t => t.Steps!.OrderBy(s => s.OccurredAt));

            return await query.FirstOrDefaultAsync(t => t.Id == id);
        }

        public Task<PaymentTransactionEntity?> GetByProviderOrderIdAsync(string providerOrderId)
            => _context.PaymentTransactions
                .FirstOrDefaultAsync(t => t.ProviderOrderId == providerOrderId);

        public Task<PaymentTransactionEntity?> GetByIdempotencyKeyAsync(string idempotencyKey)
            => _context.PaymentTransactions
                .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey);

        public async Task UpdateAsync(PaymentTransactionEntity transaction)
        {
            _context.PaymentTransactions.Update(transaction);
            await _context.SaveChangesAsync();
        }

        public async Task AddStepAsync(PaymentTransactionStepEntity step)
        {
            await _context.PaymentTransactionSteps.AddAsync(step);
            await _context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<PaymentTransactionEntity>> ListForUserAsync(
            long userId, int skip, int take, PaymentStatus? status = null)
        {
            var query = _context.PaymentTransactions
                .Where(t => t.UserId == userId);

            if (status.HasValue)
                query = query.Where(t => t.Status == status.Value);

            return await query
                .OrderByDescending(t => t.CreatedDate)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<PaymentTransactionEntity>> ListForOrganizationAsync(
            long organizationId, int skip, int take, PaymentStatus? status = null)
        {
            var query = _context.PaymentTransactions
                .Where(t => t.OrganizationId == organizationId);

            if (status.HasValue)
                query = query.Where(t => t.Status == status.Value);

            return await query
                .OrderByDescending(t => t.CreatedDate)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<PaymentTransactionEntity>> ListAllAsync(
            int skip, int take, PaymentStatus? status = null, DateTime? from = null, DateTime? to = null)
        {
            var query = _context.PaymentTransactions.AsQueryable();

            if (status.HasValue)
                query = query.Where(t => t.Status == status.Value);
            if (from.HasValue)
                query = query.Where(t => t.CreatedDate >= from.Value);
            if (to.HasValue)
                query = query.Where(t => t.CreatedDate <= to.Value);

            return await query
                .OrderByDescending(t => t.CreatedDate)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<PaymentTransactionStepEntity>> GetStepsAsync(long transactionId)
        {
            return await _context.PaymentTransactionSteps
                .Where(s => s.PaymentTransactionId == transactionId)
                .OrderBy(s => s.OccurredAt)
                .ThenBy(s => s.Id)
                .ToListAsync();
        }
    }
}
