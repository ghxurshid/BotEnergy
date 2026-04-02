using Domain.Dtos;
using Domain.Dtos.Base;

namespace Domain.Interfaces
{
    public interface IUserService
    {
        Task<GenericDto<GetUserDto>> GetCurrentUserAsync(long userId);
        Task<GenericDto<UpdateUserResultDto>> UpdateCurrentUserAsync(long userId, UpdateUserDto dto);
    }
}
