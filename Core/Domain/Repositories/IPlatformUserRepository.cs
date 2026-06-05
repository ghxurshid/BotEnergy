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
        Task<PlatformUserEntity> CreateAsync(PlatformUserEntity user);
        Task<PlatformUserEntity> UpdateAsync(PlatformUserEntity user);
        Task DeleteAsync(long userId);
    }
}
