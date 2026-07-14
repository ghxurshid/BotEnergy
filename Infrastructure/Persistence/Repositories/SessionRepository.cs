using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories
{
    public class SessionRepository : ISessionRepository
    {
        private readonly AppDbContext _context;

        public SessionRepository(AppDbContext context)
            => _context = context;

        public async Task<SessionEntity> CreateAsync(SessionEntity session)
        {
            await _context.Sessions.AddAsync(session);
            await _context.SaveChangesAsync();
            return session;
        }

        public async Task<SessionEntity?> GetByIdAsync(long sessionId)
        {
            return await _context.Sessions
                .Include(s => s.User)
                .Include(s => s.Device)
                .FirstOrDefaultAsync(s => s.Id == sessionId);
        }

        public async Task<SessionEntity?> GetByIdWithProcessesAsync(long sessionId)
        {
            return await _context.Sessions
                .Include(s => s.User)
                .Include(s => s.Device)
                .Include(s => s.Processes!.OrderBy(p => p.StartedAt))
                .FirstOrDefaultAsync(s => s.Id == sessionId);
        }

        public async Task<SessionEntity?> GetByTokenAsync(string sessionToken)
        {
            return await _context.Sessions
                .Include(s => s.User)
                .Include(s => s.Device)
                .FirstOrDefaultAsync(s => s.SessionToken == sessionToken);
        }

        public async Task<SessionEntity> UpdateAsync(SessionEntity session)
        {
            // Tracked entity'da Update() barcha ustunlarni Modified qilib yuboradi —
            // faqat detached bo'lsa attach qilamiz, aks holda change-tracking o'zi yetarli.
            if (_context.Entry(session).State == EntityState.Detached)
                _context.Sessions.Update(session);
            await _context.SaveChangesAsync();
            return session;
        }

        public async Task<List<SessionEntity>> GetIdleSessionsAsync(DateTime idleBefore)
        {
            // Settling — watcher yopadi, idle cleaner tegmasin (aks holda har 30s da qayta yopishga urinadi).
            return await _context.Sessions
                .Include(s => s.Device)
                .Include(s => s.User)
                .Include(s => s.Processes)
                .Where(s =>
                    s.Status != SessionStatus.Closed &&
                    s.Status != SessionStatus.Settling &&
                    s.LastActivityAt < idleBefore)
                .ToListAsync();
        }

        public async Task<List<SessionEntity>> GetActiveSessionsForDeviceAsync(long deviceId)
        {
            return await _context.Sessions
                .Include(s => s.Device)
                .Include(s => s.User)
                .Include(s => s.Processes)
                .Where(s =>
                    s.DeviceId == deviceId &&
                    s.Status != SessionStatus.Closed)
                .ToListAsync();
        }

        public Task<bool> HasActiveAsync(long userId, params SessionStatus[] statuses)
        {
            if (statuses is null || statuses.Length == 0)
                return Task.FromResult(false);

            return _context.Sessions
                .AnyAsync(s => s.UserId == userId && statuses.Contains(s.Status));
        }

        public async Task<SessionEntity?> GetActiveByUserAsync(long userId)
        {
            // Faqat o'qish uchun (GetCurrent/Bootstrap) — tracking keraksiz.
            return await _context.Sessions
                .AsNoTracking()
                .Include(s => s.Device)
                    .ThenInclude(d => d!.Products!.Where(p => p.IsActive))
                .Include(s => s.Processes!.OrderByDescending(p => p.StartedAt))
                .Where(s => s.UserId == userId && s.Status != SessionStatus.Closed)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<PagedResult<SessionEntity>> GetHistoryByUserAsync(long userId, PaginationParams pagination, DateTime? from = null, DateTime? to = null)
        {
            var query = _context.Sessions
                .AsNoTracking()
                .Include(s => s.Device)
                .Where(s => s.UserId == userId);

            if (from.HasValue)
                query = query.Where(s => s.CreatedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(s => s.CreatedAt <= to.Value);

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync();

            return new PagedResult<SessionEntity>
            {
                Items = items,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize,
                TotalCount = total
            };
        }

        public Task<int> TouchAsync(long sessionId)
        {
            var now = DateTime.Now;
            return _context.Sessions
                .Where(s => s.Id == sessionId && s.Status != SessionStatus.Closed)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.LastActivityAt, now)
                    .SetProperty(s => s.UpdatedDate, now));
        }
    }
}
