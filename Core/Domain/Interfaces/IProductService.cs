using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Enums;

namespace Domain.Interfaces
{
    public interface IProductService
    {
        Task<GenericDto<CreateProductResultDto>> CreateAsync(CreateProductDto dto);
        GenericDto<AllowedProductTypesResultDto> GetAllowedProductTypes(DeviceType deviceType);
    }
}
