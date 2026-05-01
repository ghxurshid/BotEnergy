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
            _context.Sessions.Update(session);
            await _context.SaveChangesAsync();
            return session;
        }

        public async Task<List<SessionEntity>> GetIdleSessionsAsync(DateTime idleBefore)
        {
            return await _context.Sessions
                .Include(s => s.Device)
                .Include(s => s.User)
                .Include(s => s.Processes)
                .Where(s =>
                    s.Status != SessionStatus.Closed &&
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
    }
}
