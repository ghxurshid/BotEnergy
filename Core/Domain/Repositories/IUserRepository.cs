using Domain.Entities;

namespace Domain.Repositories
{
    public interface IUserRepository
    {
        Task<UserEntity?> GetByPhoneNumberAsync(string phoneNumber);
        Task<UserEntity> CreateUserAsync(UserEntity user);
        Task<UserEntity> UpdateUserAsync(UserEntity user);
        Task DeleteUserAsync(long userId);
    }
}