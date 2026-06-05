using Domain.Auth;
using Domain.Dtos;
using Domain.Dtos.Base;

namespace Domain.Interfaces
{
    /// <summary>
    /// Corporate (tashkilot) foydalanuvchilarini boshqarish.
    /// Manage istalgan tashkilot uchun; Corporate bosh admin faqat o'z tashkiloti uchun.
    /// </summary>
    public interface ICustomerAdminService
    {
        Task<GenericDto<UserAdminResultDto>> CreateAsync(CreateCorporateUserDto dto, AccessScope scope);
        Task<GenericDto<PagedResult<CustomerUserItemDto>>> GetByOrganizationAsync(long organizationId, PaginationParams param, AccessScope scope);
        Task<GenericDto<CustomerUserItemDto>> GetByIdAsync(long userId, AccessScope scope);
        Task<GenericDto<UserAdminResultDto>> SetPasswordAsync(SetPasswordAdminDto dto, AccessScope scope);
        Task<GenericDto<UserAdminResultDto>> BlockAsync(long userId, AccessScope scope);
        Task<GenericDto<UserAdminResultDto>> UnblockAsync(long userId, AccessScope scope);
        Task<GenericDto<UserAdminResultDto>> DeleteAsync(long userId, AccessScope scope);
    }
}
