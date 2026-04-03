using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    public class UserAdminService : IUserAdminService
    {
        private readonly IUserRepository _userRepo;
        private readonly IRoleRepository _roleRepo;

        public UserAdminService(IUserRepository userRepo, IRoleRepository roleRepo)
        {
            _userRepo = userRepo;
            _roleRepo = roleRepo;
        }

        public async Task<GenericDto<List<UserAdminItemDto>>> GetAllAsync()
        {
            var users = await _userRepo.GetAllAsync();
            var items = users.Select(ToItem).ToList();
            return GenericDto<List<UserAdminItemDto>>.Success(items);
        }

        public async Task<GenericDto<UserAdminItemDto>> GetByIdAsync(long userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user is null)
                return GenericDto<UserAdminItemDto>.Error(404, "Foydalanuvchi topilmadi.");

            return GenericDto<UserAdminItemDto>.Success(ToItem(user));
        }

        public async Task<GenericDto<UserAdminResultDto>> BlockAsync(long userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user is null)
                return GenericDto<UserAdminResultDto>.Error(404, "Foydalanuvchi topilmadi.");

            if (user.IsBlocked)
                return GenericDto<UserAdminResultDto>.Error(400, "Foydalanuvchi allaqachon bloklangan.");

            user.IsBlocked = true;
            await _userRepo.UpdateUserAsync(user);

            return GenericDto<UserAdminResultDto>.Success(new UserAdminResultDto
            {
                ResultMessage = "Foydalanuvchi bloklandi."
            });
        }

        public async Task<GenericDto<UserAdminResultDto>> UnblockAsync(long userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user is null)
                return GenericDto<UserAdminResultDto>.Error(404, "Foydalanuvchi topilmadi.");

            if (!user.IsBlocked)
                return GenericDto<UserAdminResultDto>.Error(400, "Foydalanuvchi bloklanmagan.");

            user.IsBlocked = false;
            await _userRepo.UpdateUserAsync(user);

            return GenericDto<UserAdminResultDto>.Success(new UserAdminResultDto
            {
                ResultMessage = "Foydalanuvchi blokdan chiqarildi."
            });
        }

        public async Task<GenericDto<UserAdminResultDto>> DeleteAsync(long userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user is null)
                return GenericDto<UserAdminResultDto>.Error(404, "Foydalanuvchi topilmadi.");

            await _userRepo.DeleteUserAsync(userId);

            return GenericDto<UserAdminResultDto>.Success(new UserAdminResultDto
            {
                ResultMessage = "Foydalanuvchi o'chirildi."
            });
        }

        private static UserAdminItemDto ToItem(UserEntity u)
        {
            decimal balance = u switch
            {
                NaturalUserEntity n => n.Balance,
                LegalUserEntity l => l.Organization?.Balance ?? 0,
                _ => 0
            };

            return new UserAdminItemDto
            {
                Id = u.Id,
                PhoneNumber = u.PhoneNumber,
                Mail = u.Mail,
                UserType = u.UserType,
                IsVerified = u.IsVerified,
                IsBlocked = u.IsBlocked,
                RoleId = u.RoleId,
                RoleName = u.Role?.Name,
                Balance = balance,
                CreatedDate = u.CreatedDate,
                LastLoginDate = u.LastLoginDate
            };
        }
    }
}
