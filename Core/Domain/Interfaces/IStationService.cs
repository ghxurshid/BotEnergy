using Domain.Auth;
using Domain.Dtos;
using Domain.Dtos.Base;

namespace Domain.Interfaces
{
    public interface IStationService
    {
        Task<GenericDto<StationResultDto>> CreateAsync(CreateStationDto dto, long callerId, HashSet<string> callerPermissions);
        Task<GenericDto<PagedResult<StationItemDto>>> GetAllAsync(PaginationParams param, AccessScope scope);
        Task<GenericDto<List<StationItemDto>>> GetByMerchantAsync(long merchantId, AccessScope scope);
        Task<GenericDto<StationItemDto>> GetByIdAsync(long id, AccessScope scope);
        Task<GenericDto<StationResultDto>> UpdateAsync(long id, UpdateStationDto dto, AccessScope scope);
        Task<GenericDto<StationResultDto>> DeleteAsync(long id, AccessScope scope);
    }
}
