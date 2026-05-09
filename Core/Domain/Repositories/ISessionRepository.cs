using Domain.Dtos.Base;
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

        /// <summary>
        /// Foydalanuvchining hozirgi aktiv (Closed bo'lmagan) sessiyasini olib keladi.
        /// Resume flow va Bootstrap endpoint uchun ishlatiladi.
        /// </summary>
        Task<SessionEntity?> GetActiveByUserAsync(long userId);

        Task<PagedResult<SessionEntity>> GetHistoryByUserAsync(long userId, PaginationParams pagination, DateTime? from = null, DateTime? to = null);

        /// <summary>
        /// Faqat LastActivityAt va UpdatedDate ni yangilash — sliding idle timeout uchun.
        /// Heartbeat va telemetry hot-path da entity tracking ortiqcha bo'lgani uchun ishlatiladi.
        /// </summary>
        Task<int> TouchAsync(long sessionId);
    }
}
