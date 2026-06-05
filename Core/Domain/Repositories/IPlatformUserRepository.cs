using Domain.Dtos.Base;
using Domain.Entities;

namespace Domain.Repositories
{
    public interface IPlatformUserRepository
    {
        /// <summary>Merchant + Role navigatsiyalari bilan yuklaydi.</summary>
        Task<PlatformUserEntity?> GetByIdAsync(long userId);
        Task<PlatformUserEntity?> GetByPhoneNumberAsync(string phoneNumber);
        Task<PagedResult<PlatformUserEntity>> GetAllAsync(PaginationParams param);
        /// <summary>Berilgan merchantning operatorlari (paged).</summary>
        Task<PagedResult<PlatformUserEntity>> GetByMerchantAsync(long merchantId, PaginationParams param);
        Task<PlatformUserEntity> CreateAsync(PlatformUserEntity user);
        Task<PlatformUserEntity> UpdateAsync(PlatformUserEntity user);
        Task DeleteAsync(long userId);
    }
}
