using Domain.Dtos;
using Domain.Dtos.Base;

namespace Domain.Interfaces
{
    public interface IDeviceService
    {
        Task<GenericDto<DeviceResultDto>> RegisterAsync(RegisterDeviceDto dto, long callerId, HashSet<string> callerPermissions);
        Task<GenericDto<PagedResult<DeviceItemDto>>> GetAllAsync(PaginationParams param);
        Task<GenericDto<List<DeviceItemDto>>> GetByStationAsync(long stationId);
        Task<GenericDto<DeviceItemDto>> GetByIdAsync(long id);
        Task<GenericDto<DeviceResultDto>> UpdateAsync(long id, UpdateDeviceDto dto);
        Task<GenericDto<DeviceResultDto>> DeleteAsync(long id);
    }
}
