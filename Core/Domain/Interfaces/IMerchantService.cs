using Domain.Dtos;
using Domain.Dtos.Base;

namespace Domain.Interfaces
{
    public interface IMerchantService
    {
        Task<GenericDto<MerchantResultDto>> CreateAsync(CreateMerchantDto dto);
        Task<GenericDto<PagedResult<MerchantItemDto>>> GetAllAsync(PaginationParams param);
        Task<GenericDto<MerchantItemDto>> GetByIdAsync(long id);
        Task<GenericDto<MerchantResultDto>> UpdateAsync(long id, UpdateMerchantDto dto);
        Task<GenericDto<MerchantResultDto>> DeleteAsync(long id);
    }
}
