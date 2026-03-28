using Domain.Entities;

namespace Domain.Repositories
{
    public interface ISessionRepository
    {
        Task<UsageSessionEntity> CreateAsync(UsageSessionEntity session);
        Task<UsageSessionEntity?> GetByIdAsync(long sessionId);
        Task<UsageSessionEntity?> GetByTokenAsync(string sessionToken);
        Task<UsageSessionEntity> UpdateAsync(UsageSessionEntity session);
        Task<List<UsageSessionEntity>> GetIdleSessionsAsync(DateTime idleBefore);
    }
}
