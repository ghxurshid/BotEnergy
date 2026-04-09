using Domain.Dtos.Base;
using Domain.Dtos.Session;

namespace Domain.Interfaces
{
    public interface ISessionService
    {
        Task<GenericDto<CreateSessionResultDto>> CreateSessionAsync(CreateSessionDto dto);
        Task<GenericDto<DeviceConnectedResultDto>> DeviceConnectAsync(DeviceConnectedDto dto);
        Task<GenericDto<SetQuantityResultDto>> SetQuantityAsync(SetQuantityDto dto);
        Task<GenericDto<SessionProgressResultDto>> ReportProgressAsync(SessionProgressDto dto);
        Task<GenericDto<DeviceFinishResultDto>> DeviceFinishAsync(DeviceFinishDto dto);
        Task<GenericDto<CloseSessionResultDto>> CloseSessionByUserAsync(CloseSessionDto dto);
        Task CloseTimedOutSessionsAsync();
    }
}
