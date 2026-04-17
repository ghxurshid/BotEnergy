using Domain.Dtos;
using Domain.Dtos.Base;

namespace Domain.Interfaces
{
    public interface IUserAdminService
    {
        Task<GenericDto<UserAdminResultDto>> CreateAsync(CreateUserAdminDto dto, long callerId, HashSet<string> callerPermissions);
        Task<GenericDto<List<UserAdminItemDto>>> GetAllAsync();
        Task<GenericDto<UserAdminItemDto>> GetByIdAsync(long userId);
        Task<GenericDto<UserAdminResultDto>> SetPasswordAsync(SetPasswordAdminDto dto);
        Task<GenericDto<UserAdminResultDto>> ResetPasswordAsync(ResetPasswordAdminDto dto);
        Task<GenericDto<UserAdminResultDto>> BlockAsync(long userId);
        Task<GenericDto<UserAdminResultDto>> UnblockAsync(long userId);
        Task<GenericDto<UserAdminResultDto>> DeleteAsync(long userId);
    }
}
