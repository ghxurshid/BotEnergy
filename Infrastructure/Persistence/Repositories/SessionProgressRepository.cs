using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories
{
    public class SessionProgressRepository : ISessionProgressRepository
    {
        private readonly AppDbContext _context;

        public SessionProgressRepository(AppDbContext context)
            => _context = context;

        public async Task<SessionProgressEntity> CreateAsync(SessionProgressEntity progress)
        {
            await _context.SessionProgresses.AddAsync(progress);
            await _context.SaveChangesAsync();
            return progress;
        }

        public async Task<List<SessionProgressEntity>> GetBySessionIdAsync(long sessionId)
        {
            return await _context.SessionProgresses
                .Where(p => p.SessionId == sessionId && !p.IsDeleted)
                .OrderBy(p => p.ReportedAt)
                .ToListAsync();
        }
    }
}
