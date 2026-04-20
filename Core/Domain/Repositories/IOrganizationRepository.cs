using Domain.Dtos.Base;
using Domain.Entities;

namespace Domain.Repositories
{
    public interface IOrganizationRepository
    {
        Task<OrganizationEntity?> GetByIdAsync(long id);
        Task<PagedResult<OrganizationEntity>> GetAllAsync(PaginationParams param);
        Task<OrganizationEntity> CreateAsync(OrganizationEntity organization);
        Task<OrganizationEntity> UpdateAsync(OrganizationEntity organization);
        Task DeleteAsync(long id);
    }
}
