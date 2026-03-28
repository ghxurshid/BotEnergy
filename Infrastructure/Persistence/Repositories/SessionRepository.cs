using Domain.Entities;
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

        public async Task<UsageSessionEntity> CreateAsync(UsageSessionEntity session)
        {
            await _context.UsageSessions.AddAsync(session);
            await _context.SaveChangesAsync();
            return session;
        }

        public async Task<UsageSessionEntity?> GetByIdAsync(long sessionId)
        {
            return await _context.UsageSessions
                .Include(s => s.User)
                .Include(s => s.Device)
                .FirstOrDefaultAsync(s => s.Id == sessionId && !s.IsDeleted);
        }

        public async Task<UsageSessionEntity?> GetByTokenAsync(string sessionToken)
        {
            return await _context.UsageSessions
                .Include(s => s.User)
                .Include(s => s.Device)
                .FirstOrDefaultAsync(s => s.SessionToken == sessionToken && !s.IsDeleted);
        }

        public async Task<UsageSessionEntity> UpdateAsync(UsageSessionEntity session)
        {
            _context.UsageSessions.Update(session);
            await _context.SaveChangesAsync();
            return session;
        }

        public async Task<List<UsageSessionEntity>> GetIdleSessionsAsync(DateTime idleBefore)
        {
            return await _context.UsageSessions
                .Where(s =>
                    !s.IsDeleted &&
                    (s.Status == SessionStatus.Pending ||
                     s.Status == SessionStatus.DeviceConnected ||
                     s.Status == SessionStatus.InProgress) &&
                    s.LastActivityAt < idleBefore)
                .ToListAsync();
        }
    }
}
