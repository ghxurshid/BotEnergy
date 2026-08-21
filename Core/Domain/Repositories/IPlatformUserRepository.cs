using Domain.Dtos.Base;
using Domain.Entities;

namespace Domain.Repositories
{
    public interface IPlatformUserRepository
    {
        /// <summary>Merchant + Role navigatsiyalari bilan yuklaydi.</summary>
        Task<PlatformUserEntity?> GetByIdAsync(long userId);
        Task<PlatformUserEntity?> GetByPhoneNumberAsync(string phoneNumber);

        /// <summary>
        /// Pochta band-emasligini tekshiradi (mail ustunida unique indeks bor).
        /// <paramref name="excludeUserId"/> — profilni tahrirlashda o'z qiymatini
        /// dublikat deb hisoblamaslik uchun.
        /// </summary>
        Task<bool> ExistsByMailAsync(string mail, long? excludeUserId = null);
        /// <summary><paramref name="excludeUserId"/> berilsa (masalan, caller'ning o'zi), ro'yxatdan chiqariladi.</summary>
        Task<PagedResult<PlatformUserEntity>> GetAllAsync(PaginationParams param, long? excludeUserId = null);
        /// <summary>Berilgan merchantning operatorlari (paged). <paramref name="excludeUserId"/> berilsa ro'yxatdan chiqariladi.</summary>
        Task<PagedResult<PlatformUserEntity>> GetByMerchantAsync(long merchantId, PaginationParams param, long? excludeUserId = null);
        Task<PlatformUserEntity> CreateAsync(PlatformUserEntity user);
        Task<PlatformUserEntity> UpdateAsync(PlatformUserEntity user);
        Task DeleteAsync(long userId);
    }
}
