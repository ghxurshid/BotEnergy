using Domain.Dtos.Base;
using Domain.Dtos.Session;

namespace Domain.Interfaces
{
    public interface ISessionService
    {
        /// <summary>
        /// Pending sessiya yaratadi va cache'ga (30 min TTL) qo'yadi.
        /// DB'ga yozilmaydi — sessiya DeviceApi tomonidan qurilma ulanganda yaratiladi.
        /// User'da DB'da aktiv sessiya bo'lsa 409 qaytaradi. Cache'da mavjud pending bor bo'lsa
        /// xuddi shu token idempotent qaytariladi.
        /// </summary>
        Task<GenericDto<CreateSessionResultDto>> CreateSessionAsync(CreateSessionDto dto);

        /// <summary>
        /// DeviceApi DB'da sessiya yaratganidan keyin RabbitMQ orqali kelgan "connected" event'ni
        /// SignalR'ga uzatadi. Bu metod DB'ni o'zgartirmaydi — sessiya allaqachon Connected statusda.
        /// </summary>
        Task<GenericDto<DeviceConnectedResultDto>> NotifyDeviceConnectedAsync(string sessionToken);

        Task<GenericDto<CloseSessionResultDto>> CloseSessionByUserAsync(CloseSessionDto dto);
        Task CloseTimedOutSessionsAsync();

        /// <summary>Offline qurilmalarning aktiv sessiyalarini Paused qiladi (yopmaydi).</summary>
        Task PauseOfflineDeviceSessionsAsync();

        /// <summary>Qurilma qayta ulanganda Paused sessiyalarni Resume qiladi.</summary>
        Task ResumePausedSessionsForDeviceAsync(long deviceId);

        /// <summary>
        /// Foydalanuvchining hozirgi aktiv sessiyasini snapshot sifatida qaytaradi.
        /// Aktiv sessiya yo'q bo'lsa null Result bilan Success qaytadi (404 emas — bu normal hol).
        /// Pending (cache) sessiyalarni ko'rsatmaydi — faqat DB'dagi aktivlar.
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
