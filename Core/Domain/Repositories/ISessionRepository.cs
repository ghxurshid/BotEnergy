using Domain.Entities;
using Domain.Enums;

namespace Domain.Repositories
{
    public interface ISessionRepository
    {
        Task<SessionEntity> CreateAsync(SessionEntity session);
        Task<SessionEntity?> GetByIdAsync(long sessionId);
        Task<SessionEntity?> GetByIdWithProcessesAsync(long sessionId);
        Task<SessionEntity?> GetByTokenAsync(string sessionToken);
        Task<SessionEntity> UpdateAsync(SessionEntity session);
        Task<List<SessionEntity>> GetIdleSessionsAsync(DateTime idleBefore);
        Task<List<SessionEntity>> GetActiveSessionsForDeviceAsync(long deviceId);
        Task<bool> HasActiveAsync(long userId, params SessionStatus[] statuses);
    }
}
