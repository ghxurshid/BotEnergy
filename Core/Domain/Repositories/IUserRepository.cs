using Domain.Entities;

namespace Domain.Repositories
{
    public interface IUserRepository
    {
        Task<UserEntity?> GetByIdAsync(long userId);
        Task<UserEntity?> GetByPhoneNumberAsync(string phoneNumber);
        Task<List<UserEntity>> GetAllAsync();
        Task<UserEntity> CreateUserAsync(UserEntity user);
        Task<UserEntity> UpdateUserAsync(UserEntity user);
        Task DeleteUserAsync(long userId);
    }
}