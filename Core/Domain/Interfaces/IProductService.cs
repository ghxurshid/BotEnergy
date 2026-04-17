using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Enums;

namespace Domain.Interfaces
{
    public interface IProductService
    {
        Task<GenericDto<ProductResultDto>> CreateAsync(CreateProductDto dto, long callerId, HashSet<string> callerPermissions);
        Task<GenericDto<List<ProductItemDto>>> GetAllAsync();
        Task<GenericDto<List<ProductItemDto>>> GetByDeviceAsync(long deviceId);
        Task<GenericDto<ProductItemDto>> GetByIdAsync(long id);
        Task<GenericDto<ProductResultDto>> UpdateAsync(long id, UpdateProductDto dto);
        Task<GenericDto<ProductResultDto>> DeleteAsync(long id);
        GenericDto<AllowedProductTypesResultDto> GetAllowedProductTypes(DeviceType deviceType);
    }
}
