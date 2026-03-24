using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Repositories;
using Persistence.Context;
using AppDbContext = Persistence.Context.AppDbContext;

namespace Persistence.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;

        public UserRepository(AppDbContext context)
            => _context = context;

        public GenericDto<UserEntity> GetByPhoneNumber(string phoneNumber)
        {
            var user = _context.Users
                .FirstOrDefault(u => u.PhoneNumber == phoneNumber);

            return user is null
                ? GenericDto<UserEntity>.Error(404, "Foydalanuvchi topilmadi.")
                : GenericDto<UserEntity>.Success(user);
        }

        public GenericDto<UserEntity> CreateUser(UserEntity user)
        {
            var exists = _context.Users
                .Any(u => u.PhoneNumber == user.PhoneNumber);

            if (exists)
                return GenericDto<UserEntity>.Error(409, "Bu telefon raqam allaqachon ro'yxatdan o'tgan.");

            _context.Users.Add(user);
            _context.SaveChanges();

            return GenericDto<UserEntity>.Success(user);
        }

        public GenericDto<UserEntity> UpdateUser(UserEntity user)
        {
            var existing = _context.Users
                .FirstOrDefault(u => u.Id == user.Id);

            if (existing is null)
                return GenericDto<UserEntity>.Error(404, "Foydalanuvchi topilmadi.");

            existing.Mail = user.Mail;
            existing.PhoneNumber = user.PhoneNumber;
            existing.Balance = user.Balance;
            existing.IsBlocked = user.IsBlocked;
            existing.IsVerified = user.IsVerified;
            existing.LastLoginDate = user.LastLoginDate;
            existing.LastActiveDate = user.LastActiveDate;
            existing.PasswordHash = user.PasswordHash;
            existing.PasswordSalt = user.PasswordSalt;

            _context.SaveChanges();

            return GenericDto<UserEntity>.Success(existing);
        }

        public GenericDto<UserEntity> DeleteUser(long userId)
        {
            var user = _context.Users
                .FirstOrDefault(u => u.Id == userId);

            if (user is null)
                return GenericDto<UserEntity>.Error(404, "Foydalanuvchi topilmadi.");

            user.IsDeleted = true;
            _context.SaveChanges();

            return GenericDto<UserEntity>.Success(user);
        }
    }
}
