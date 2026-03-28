using Domain.Dtos;
using Domain.Dtos.Base;

namespace Domain.Interfaces
{
    public interface IUserService
    {
        Task<GenericDto<GetUserDto>> GetCurrentUserAsync(string phoneNumber);
        Task<GenericDto<UpdateUserResultDto>> UpdateCurrentUserAsync(string phoneNumber, UpdateUserDto dto);
    }
}
