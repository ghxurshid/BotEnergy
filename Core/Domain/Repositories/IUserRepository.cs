using Domain.Dtos.Base;
using Domain.Entities;

namespace Domain.Repositories
{
    public interface IUserRepository
    {
        Task<UserEntity?> GetByIdAsync(long userId);
        Task<UserEntity?> GetByPhoneNumberAsync(string phoneNumber);
        Task<PagedResult<UserEntity>> GetAllAsync(PaginationParams param);
        Task<UserEntity> CreateUserAsync(UserEntity user);
        Task<UserEntity> UpdateUserAsync(UserEntity user);
        Task DeleteUserAsync(long userId);
    }
}