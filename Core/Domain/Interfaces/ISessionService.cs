using Domain.Dtos.Base;
using Domain.Dtos.Session;

namespace Domain.Interfaces
{
    public interface ISessionService
    {
        Task<GenericDto<CreateSessionResultDto>> CreateSessionAsync(CreateSessionDto dto);
        Task<GenericDto<DeviceConnectedResultDto>> DeviceConnectAsync(DeviceConnectedDto dto);
        Task<GenericDto<CloseSessionResultDto>> CloseSessionByUserAsync(CloseSessionDto dto);
        Task CloseTimedOutSessionsAsync();
        Task CloseOfflineDeviceSessionsAsync();

        /// <summary>
        /// Foydalanuvchining hozirgi aktiv sessiyasini snapshot sifatida qaytaradi.
        /// Aktiv sessiya yo'q bo'lsa null Result bilan Success qaytadi (404 emas — bu normal hol).
        /// </summary>
        Task<GenericDto<CurrentSessionDto?>> GetCurrentAsync(long userId);

        /// <summary>
        /// Sessiyaning LastActivityAt ni yangilaydi (sliding idle timeout).
        /// </summary>
        Task<GenericDto<HeartbeatResultDto>> HeartbeatAsync(long sessionId, long userId);

        Task<GenericDto<PagedResult<SessionHistoryItemDto>>> GetHistoryAsync(long userId, PaginationParams pagination, DateTime? from, DateTime? to);
        Task<GenericDto<CurrentSessionDto>> GetByIdAsync(long sessionId, long userId);
    }
}
