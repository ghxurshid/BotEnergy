using Domain.Auth;
using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Enums;

namespace Domain.Interfaces
{
    public interface IProductService
    {
        Task<GenericDto<ProductResultDto>> CreateAsync(CreateProductDto dto, long callerId, HashSet<string> callerPermissions);
        Task<GenericDto<PagedResult<ProductItemDto>>> GetAllAsync(PaginationParams param, AccessScope scope);
        Task<GenericDto<List<ProductItemDto>>> GetByDeviceAsync(long deviceId, AccessScope scope);
        Task<GenericDto<ProductItemDto>> GetByIdAsync(long id, AccessScope scope);
        Task<GenericDto<ProductResultDto>> UpdateAsync(long id, UpdateProductDto dto, AccessScope scope);
        Task<GenericDto<ProductResultDto>> DeleteAsync(long id, AccessScope scope);
        GenericDto<AllowedProductTypesResultDto> GetAllowedProductTypes(DeviceType deviceType);
    }
}
