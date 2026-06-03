using Domain.Auth;
using Domain.Dtos;
using Domain.Dtos.Base;

namespace Domain.Interfaces
{
    public interface IDeviceService
    {
        Task<GenericDto<DeviceResultDto>> RegisterAsync(RegisterDeviceDto dto, long callerId, HashSet<string> callerPermissions);
        Task<GenericDto<PagedResult<DeviceItemDto>>> GetAllAsync(PaginationParams param, AccessScope scope);
        Task<GenericDto<List<DeviceItemDto>>> GetByStationAsync(long stationId, AccessScope scope);
        Task<GenericDto<DeviceItemDto>> GetByIdAsync(long id, AccessScope scope);
        Task<GenericDto<DeviceResultDto>> UpdateAsync(long id, UpdateDeviceDto dto, AccessScope scope);
        Task<GenericDto<DeviceResultDto>> DeleteAsync(long id, AccessScope scope);
    }
}
