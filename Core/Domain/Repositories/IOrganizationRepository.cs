using Domain.Dtos.Base;
using Domain.Entities;

namespace Domain.Repositories
{
    public interface IOrganizationRepository
    {
        Task<OrganizationEntity?> GetByIdAsync(long id);
        Task<PagedResult<OrganizationEntity>> GetAllAsync(PaginationParams param, long? organizationId = null);
        Task<OrganizationEntity> CreateAsync(OrganizationEntity organization);
        Task<OrganizationEntity> UpdateAsync(OrganizationEntity organization);
        Task DeleteAsync(long id);

        /// <summary>
        /// Tashkilot balansidan atomik yechish: min(balans, <paramref name="maxAmount"/>) yechiladi,
        /// yechilgan miqdor qaytadi (FOR UPDATE lock). Tashkilot topilmasa 0.
        /// </summary>
        Task<decimal> DeductBalanceAsync(long organizationId, decimal maxAmount);

        /// <summary>Balansni atomik to'ldirish. Yangi balans qaytadi; tashkilot topilmasa null.</summary>
        Task<decimal?> TopUpBalanceAsync(long organizationId, decimal amount);
    }
}
