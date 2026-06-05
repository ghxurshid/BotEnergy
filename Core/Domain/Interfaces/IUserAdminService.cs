using Domain.Auth;
using Domain.Dtos;
using Domain.Dtos.Base;

namespace Domain.Interfaces
{
    /// <summary>
    /// Platform foydalanuvchilarni (Manage/Merchant) boshqarish.
    /// Manage — barchasini; Merchant operator — faqat o'z merchanti operatorlarini.
    /// </summary>
    public interface IUserAdminService
    {
        Task<GenericDto<UserAdminResultDto>> CreateAsync(CreateUserAdminDto dto, AccessScope scope);
        Task<GenericDto<PagedResult<UserAdminItemDto>>> GetAllAsync(PaginationParams param, AccessScope scope);
        Task<GenericDto<UserAdminItemDto>> GetByIdAsync(long userId, AccessScope scope);
        Task<GenericDto<UserAdminResultDto>> SetPasswordAsync(SetPasswordAdminDto dto, AccessScope scope);
        Task<GenericDto<UserAdminResultDto>> ResetPasswordAsync(ResetPasswordAdminDto dto, AccessScope scope);
        Task<GenericDto<UserAdminResultDto>> BlockAsync(long userId, AccessScope scope);
        Task<GenericDto<UserAdminResultDto>> UnblockAsync(long userId, AccessScope scope);
        Task<GenericDto<UserAdminResultDto>> DeleteAsync(long userId, AccessScope scope);
    }
}
