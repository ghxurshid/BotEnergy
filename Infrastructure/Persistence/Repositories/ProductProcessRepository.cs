using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories
{
    public class ProductProcessRepository : IProductProcessRepository
    {
        private readonly AppDbContext _context;

        public ProductProcessRepository(AppDbContext context)
            => _context = context;

        public async Task<ProductProcessEntity> CreateAsync(ProductProcessEntity process)
        {
            await _context.ProductProcesses.AddAsync(process);
            await _context.SaveChangesAsync();
            return process;
        }

        public async Task<ProductProcessEntity?> GetByIdAsync(long id)
        {
            return await _context.ProductProcesses
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<ProductProcessEntity?> GetByIdWithSessionAsync(long id)
        {
            return await _context.ProductProcesses
                .Include(p => p.Session!).ThenInclude(s => s!.Device)
                .Include(p => p.Session!).ThenInclude(s => s!.User)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<ProductProcessEntity?> GetActiveBySessionTokenAsync(string sessionToken)
        {
            return await _context.ProductProcesses
                .Include(p => p.Session!).ThenInclude(s => s!.Device)
                .Include(p => p.Session!).ThenInclude(s => s!.User)
                .Where(p =>
                    p.Session!.SessionToken == sessionToken &&
                    (p.Status == ProcessStatus.Started ||
                     p.Status == ProcessStatus.InProcess ||
                     p.Status == ProcessStatus.Paused))
                .OrderByDescending(p => p.StartedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<ProductProcessEntity> UpdateAsync(ProductProcessEntity process)
        {
            _context.ProductProcesses.Update(process);
            await _context.SaveChangesAsync();
            return process;
        }

        public Task<bool> HasActiveProcessAsync(long sessionId)
        {
            return _context.ProductProcesses
                .AnyAsync(p =>
                    p.SessionId == sessionId &&
                    (p.Status == ProcessStatus.Started ||
                     p.Status == ProcessStatus.InProcess ||
                     p.Status == ProcessStatus.Paused));
        }

        /// <summary>
        /// Atomic SQL-level UPDATE — race-safe.
        /// Faqat aktiv (Started/InProcess) jarayonlarda va incoming sequence eski sequence-dan
        /// katta bo'lganda bajariladi (idempotency).
        /// </summary>
        public Task<int> IncrementGivenAmountAsync(long processId, decimal delta, long sequence)
        {
            return _context.ProductProcesses
                .Where(p => p.Id == processId &&
                            (p.Status == ProcessStatus.Started || p.Status == ProcessStatus.InProcess) &&
                            p.LastTelemetrySequence < sequence)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.GivenAmount, p => p.GivenAmount + delta)
                    .SetProperty(p => p.LastTelemetrySequence, sequence)
                    .SetProperty(p => p.UpdatedDate, DateTime.Now));
        }
    }
}
