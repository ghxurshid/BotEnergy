using Domain.Dtos;
using Domain.Dtos.Base;

namespace Domain.Interfaces
{
    public interface IOrganizationService
    {
        Task<GenericDto<OrganizationResultDto>> CreateAsync(CreateOrganizationDto dto);
        Task<GenericDto<PagedResult<OrganizationItemDto>>> GetAllAsync(PaginationParams param);
        Task<GenericDto<OrganizationItemDto>> GetByIdAsync(long id);
        Task<GenericDto<OrganizationResultDto>> UpdateAsync(long id, UpdateOrganizationDto dto);
        Task<GenericDto<OrganizationResultDto>> DeleteAsync(long id);
    }
}
