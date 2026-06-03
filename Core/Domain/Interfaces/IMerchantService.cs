using Domain.Auth;
using Domain.Dtos;
using Domain.Dtos.Base;

namespace Domain.Interfaces
{
    public interface IMerchantService
    {
        Task<GenericDto<MerchantResultDto>> CreateAsync(CreateMerchantDto dto);
        Task<GenericDto<PagedResult<MerchantItemDto>>> GetAllAsync(PaginationParams param, AccessScope scope);
        Task<GenericDto<MerchantItemDto>> GetByIdAsync(long id, AccessScope scope);
        Task<GenericDto<MerchantResultDto>> UpdateAsync(long id, UpdateMerchantDto dto, AccessScope scope);
        Task<GenericDto<MerchantResultDto>> DeleteAsync(long id, AccessScope scope);
    }
}
