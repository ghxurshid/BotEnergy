using Domain.Auth;
using Domain.Dtos;
using Domain.Dtos.Base;

namespace Domain.Interfaces
{
    public interface IOrganizationService
    {
        Task<GenericDto<OrganizationResultDto>> CreateAsync(CreateOrganizationDto dto);
        Task<GenericDto<PagedResult<OrganizationItemDto>>> GetAllAsync(PaginationParams param, AccessScope scope);
        Task<GenericDto<OrganizationItemDto>> GetByIdAsync(long id, AccessScope scope);
        Task<GenericDto<OrganizationResultDto>> UpdateAsync(long id, UpdateOrganizationDto dto, AccessScope scope);
        Task<GenericDto<OrganizationResultDto>> DeleteAsync(long id, AccessScope scope);
    }
}
