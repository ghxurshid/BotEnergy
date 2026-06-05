using Domain.Dtos.Base;
using Domain.Entities;

namespace Domain.Repositories
{
    public interface ICustomerUserRepository
    {
        /// <summary>Organization + Role navigatsiyalari bilan yuklaydi.</summary>
        Task<CustomerUserEntity?> GetByIdAsync(long userId);
        Task<CustomerUserEntity?> GetByPhoneNumberAsync(string phoneNumber);
        Task<PagedResult<CustomerUserEntity>> GetAllAsync(PaginationParams param);
        /// <summary>Berilgan tashkilotning corporate userlari (paged).</summary>
        Task<PagedResult<CustomerUserEntity>> GetByOrganizationAsync(long organizationId, PaginationParams param);
        Task<CustomerUserEntity> CreateAsync(CustomerUserEntity user);
        Task<CustomerUserEntity> UpdateAsync(CustomerUserEntity user);
        Task DeleteAsync(long userId);
    }
}
