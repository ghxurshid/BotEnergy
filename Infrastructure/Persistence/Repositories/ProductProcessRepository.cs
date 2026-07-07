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
            if (_context.Entry(process).State == EntityState.Detached)
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

        public async Task<List<ProductProcessEntity>> GetStalledProcessesAsync(DateTime staleBefore)
        {
            return await _context.ProductProcesses
                .Include(p => p.Session!).ThenInclude(s => s!.Device)
                .Include(p => p.Session!).ThenInclude(s => s!.User)
                .Where(p =>
                    (p.Status == ProcessStatus.Started || p.Status == ProcessStatus.InProcess) &&
                    p.UpdatedDate < staleBefore &&
                    p.Session!.Status != SessionStatus.Closed)
                .ToListAsync();
        }

        /// <summary>
        /// Atomic SQL-level UPDATE — race-safe.
        /// GivenAmount qurilmadan kelgan cumulative qiymatga o'rnatiladi (delta emas).
        /// Status ham InProcess'ga o'tkaziladi (Started bo'lsa).
        /// Faqat aktiv (Started/InProcess) jarayonlarda va incoming sequence eski sequence-dan
        /// katta bo'lganda bajariladi (idempotency).
        /// </summary>
        public Task<int> SetGivenAmountAsync(long processId, decimal totalGiven, long sequence)
        {
            var now = DateTime.Now;
            return _context.ProductProcesses
                .Where(p => p.Id == processId &&
                            (p.Status == ProcessStatus.Started || p.Status == ProcessStatus.InProcess) &&
                            p.LastTelemetrySequence < sequence)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.GivenAmount, totalGiven)
                    .SetProperty(p => p.LastTelemetrySequence, sequence)
                    .SetProperty(p => p.Status, ProcessStatus.InProcess)
                    .SetProperty(p => p.UpdatedDate, now));
        }

        /// <summary>
        /// Atomic completion — race-safe. Bitta thread yutadi (boshqalari 0 qaytaradi).
        /// </summary>
        public Task<int> CompleteProcessAsync(long processId, decimal totalGiven, ProcessEndReason endReason, DateTime endedAt)
        {
            return _context.ProductProcesses
                .Where(p => p.Id == processId && p.Status != ProcessStatus.Ended)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.GivenAmount, totalGiven)
                    .SetProperty(p => p.Status, ProcessStatus.Ended)
                    .SetProperty(p => p.EndReason, (ProcessEndReason?)endReason)
                    .SetProperty(p => p.EndedAt, (DateTime?)endedAt)
                    .SetProperty(p => p.UpdatedDate, endedAt));
        }

        public Task ReloadAsync(ProductProcessEntity process)
            => _context.Entry(process).ReloadAsync();

        public async Task<bool> TryClaimBalanceDeductionAsync(long processId)
        {
            var now = DateTime.Now;
            var affected = await _context.ProductProcesses
                .Where(p => p.Id == processId && !p.IsBalanceDeducted)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.IsBalanceDeducted, true)
                    .SetProperty(p => p.UpdatedDate, now));

            return affected > 0;
        }
    }
}
