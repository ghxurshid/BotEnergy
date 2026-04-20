using Domain.Dtos.Base;
using Domain.Entities;

namespace Domain.Repositories
{
    public interface IMerchantRepository
    {
        Task<MerchantEntity?> GetByIdAsync(long id);
        Task<PagedResult<MerchantEntity>> GetAllAsync(PaginationParams param);
        Task<MerchantEntity?> GetByPhoneNumberAsync(string phoneNumber);
        Task<MerchantEntity> CreateAsync(MerchantEntity merchant);
        Task<MerchantEntity> UpdateAsync(MerchantEntity merchant);
        Task DeleteAsync(long id);
    }
}
