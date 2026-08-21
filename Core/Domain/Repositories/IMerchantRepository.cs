using Domain.Dtos.Base;
using Domain.Entities;

namespace Domain.Repositories
{
    public interface IMerchantRepository
    {
        Task<MerchantEntity?> GetByIdAsync(long id);
        Task<PagedResult<MerchantEntity>> GetAllAsync(PaginationParams param, long? merchantId = null);
        Task<MerchantEntity?> GetByPhoneNumberAsync(string phoneNumber);

        /// <summary>INN band-emasligini tekshiradi (inn ustunida unique indeks bor).</summary>
        Task<bool> ExistsByInnAsync(string inn, long? excludeMerchantId = null);
        Task<MerchantEntity> CreateAsync(MerchantEntity merchant);
        Task<MerchantEntity> UpdateAsync(MerchantEntity merchant);
        Task DeleteAsync(long id);
    }
}
