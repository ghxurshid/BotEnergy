using Domain.Entities;

namespace Domain.Repositories
{
    public interface ISessionProgressRepository
    {
        Task<SessionProgressEntity> CreateAsync(SessionProgressEntity progress);
        Task<List<SessionProgressEntity>> GetBySessionIdAsync(long sessionId);
    }
}
