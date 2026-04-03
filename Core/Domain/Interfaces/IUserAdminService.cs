using Domain.Dtos;
using Domain.Dtos.Base;

namespace Domain.Interfaces
{
    public interface IUserAdminService
    {
        Task<GenericDto<List<UserAdminItemDto>>> GetAllAsync();
        Task<GenericDto<UserAdminItemDto>> GetByIdAsync(long userId);
        Task<GenericDto<UserAdminResultDto>> BlockAsync(long userId);
        Task<GenericDto<UserAdminResultDto>> UnblockAsync(long userId);
        Task<GenericDto<UserAdminResultDto>> DeleteAsync(long userId);
    }
}
