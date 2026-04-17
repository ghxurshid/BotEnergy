using Domain.Dtos;
using Domain.Dtos.Base;

namespace Domain.Interfaces
{
    public interface IStationService
    {
        Task<GenericDto<StationResultDto>> CreateAsync(CreateStationDto dto, long callerId, HashSet<string> callerPermissions);
        Task<GenericDto<List<StationItemDto>>> GetAllAsync();
        Task<GenericDto<List<StationItemDto>>> GetByMerchantAsync(long merchantId);
        Task<GenericDto<StationItemDto>> GetByIdAsync(long id);
        Task<GenericDto<StationResultDto>> UpdateAsync(long id, UpdateStationDto dto);
        Task<GenericDto<StationResultDto>> DeleteAsync(long id);
    }
}
