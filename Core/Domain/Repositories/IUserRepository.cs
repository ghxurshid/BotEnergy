using Domain.Dtos.Base;
using Domain.Entities;

namespace Domain.Repositories
{
    public interface IUserRepository
    {
        GenericDto<UserEntity> GetByPhoneNumber(string phoneNumber);
        GenericDto<UserEntity> CreateUser(UserEntity user);
        GenericDto<UserEntity> UpdateUser(UserEntity user);
        GenericDto<UserEntity> DeleteUser(string userId);
    }
}
