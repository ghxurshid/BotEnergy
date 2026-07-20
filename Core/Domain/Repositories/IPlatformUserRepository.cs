using Domain.Dtos.Base;
using Domain.Entities;

namespace Domain.Repositories
{
    public interface IPlatformUserRepository
    {
        /// <summary>Merchant + Role navigatsiyalari bilan yuklaydi.</summary>
        Task<PlatformUserEntity?> GetByIdAsync(long userId);
        Task<PlatformUserEntity?> GetByPhoneNumberAsync(string phoneNumber);
        /// <summary><paramref name="excludeUserId"/> berilsa (masalan, caller'ning o'zi), ro'yxatdan chiqariladi.</summary>
        Task<PagedResult<PlatformUserEntity>> GetAllAsync(PaginationParams param, long? excludeUserId = null);
        /// <summary>Berilgan merchantning operatorlari (paged). <paramref name="excludeUserId"/> berilsa ro'yxatdan chiqariladi.</summary>
        Task<PagedResult<PlatformUserEntity>> GetByMerchantAsync(long merchantId, PaginationParams param, long? excludeUserId = null);
        Task<PlatformUserEntity> CreateAsync(PlatformUserEntity user);
        Task<PlatformUserEntity> UpdateAsync(PlatformUserEntity user);
        Task DeleteAsync(long userId);
    }
}
