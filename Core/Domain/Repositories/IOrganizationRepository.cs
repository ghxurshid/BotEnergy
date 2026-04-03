using Domain.Entities;

namespace Domain.Repositories
{
    public interface IOrganizationRepository
    {
        Task<OrganizationEntity?> GetByIdAsync(long id);
        Task<List<OrganizationEntity>> GetAllAsync();
        Task<OrganizationEntity> CreateAsync(OrganizationEntity organization);
        Task<OrganizationEntity> UpdateAsync(OrganizationEntity organization);
        Task DeleteAsync(long id);
    }
}
