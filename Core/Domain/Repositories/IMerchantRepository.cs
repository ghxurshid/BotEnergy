using Domain.Entities;

namespace Domain.Repositories
{
    public interface IMerchantRepository
    {
        Task<MerchantEntity?> GetByIdAsync(long id);
        Task<List<MerchantEntity>> GetAllAsync();
        Task<MerchantEntity?> GetByPhoneNumberAsync(string phoneNumber);
        Task<MerchantEntity> CreateAsync(MerchantEntity merchant);
        Task<MerchantEntity> UpdateAsync(MerchantEntity merchant);
        Task DeleteAsync(long id);
    }
}
