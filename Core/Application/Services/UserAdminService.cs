using Application.Helpers;
using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    /// <summary>
    /// Platform foydalanuvchilarni (Manage/Merchant) boshqarish — faqat Manage tomonidan.
    /// </summary>
    public class UserAdminService : IUserAdminService
    {
        private readonly IPlatformUserRepository _userRepo;
        private readonly IPlatformRoleRepository _roleRepo;
        private readonly IMerchantRepository _merchantRepo;

        public UserAdminService(
            IPlatformUserRepository userRepo,
            IPlatformRoleRepository roleRepo,
            IMerchantRepository merchantRepo)
        {
            _userRepo = userRepo;
            _roleRepo = roleRepo;
            _merchantRepo = merchantRepo;
        }

        public async Task<GenericDto<UserAdminResultDto>> CreateAsync(CreateUserAdminDto dto, long callerId, HashSet<string> callerPermissions)
        {
            var existingUser = await _userRepo.GetByPhoneNumberAsync(dto.PhoneNumber);
            if (existingUser is not null)
                return GenericDto<UserAdminResultDto>.Error(409, "Bu telefon raqam bilan platform foydalanuvchi allaqachon mavjud.");

            var role = await _roleRepo.GetByIdAsync(dto.RoleId);
            if (role is null)
                return GenericDto<UserAdminResultDto>.Error(404, "Rol topilmadi.");

            long? merchantId = null;

            if (dto.Type == PlatformUserType.Merchant)
            {
                if (dto.MerchantId is null)
                    return GenericDto<UserAdminResultDto>.Error(400, "Merchant foydalanuvchi uchun MerchantId majburiy.");

                var merchant = await _merchantRepo.GetByIdAsync(dto.MerchantId.Value);
                if (merchant is null)
                    return GenericDto<UserAdminResultDto>.Error(404, "Merchant topilmadi.");
                if (!merchant.IsActive)
                    return GenericDto<UserAdminResultDto>.Error(400, "Merchant faol emas.");

                // Rol shu merchantga tegishli (scoped) bo'lishi kerak.
                if (role.MerchantId != dto.MerchantId.Value)
                    return GenericDto<UserAdminResultDto>.Error(400, "Tanlangan rol ushbu merchantga tegishli bo'lishi kerak.");

                merchantId = dto.MerchantId;
            }
            else
            {
                // Manage → rol global (MerchantId null) bo'lishi kerak.
                if (role.MerchantId is not null)
                    return GenericDto<UserAdminResultDto>.Error(400, "Manage foydalanuvchiga faqat global (Manage) rol biriktiriladi.");
            }

            var newUser = new PlatformUserEntity
            {
                Type = dto.Type,
                PhoneId = dto.PhoneId,
                PhoneNumber = dto.PhoneNumber,
                Mail = dto.Mail,
                RoleId = dto.RoleId,
                MerchantId = merchantId,
                IsOtpVerified = true,
                IsVerified = false
            };

            var created = await _userRepo.CreateAsync(newUser);

            return GenericDto<UserAdminResultDto>.Success(new UserAdminResultDto
            {
                Id = created.Id,
                ResultMessage = "Platform foydalanuvchi muvaffaqiyatli yaratildi."
            });
        }

        public async Task<GenericDto<UserAdminResultDto>> SetPasswordAsync(SetPasswordAdminDto dto)
        {
            var user = await _userRepo.GetByIdAsync(dto.UserId);
            if (user is null)
                return GenericDto<UserAdminResultDto>.Error(404, "Foydalanuvchi topilmadi.");

            if (user.IsVerified)
                return GenericDto<UserAdminResultDto>.Error(400, "Parol allaqachon o'rnatilgan.");

            if (!user.IsOtpVerified)
                return GenericDto<UserAdminResultDto>.Error(400, "OTP tasdiqlanmagan.");

            var (hash, salt) = PasswordHelper.CreatePassword(dto.Password);
            user.PasswordHash = hash;
            user.PasswordSalt = salt;
            user.IsVerified = true;
            await _userRepo.UpdateAsync(user);

            return GenericDto<UserAdminResultDto>.Success(new UserAdminResultDto
            {
                Id = user.Id,
                ResultMessage = "Parol muvaffaqiyatli o'rnatildi."
            });
        }

        public async Task<GenericDto<UserAdminResultDto>> ResetPasswordAsync(ResetPasswordAdminDto dto)
        {
            var user = await _userRepo.GetByIdAsync(dto.UserId);
            if (user is null)
                return GenericDto<UserAdminResultDto>.Error(404, "Foydalanuvchi topilmadi.");

            if (!user.IsVerified)
                return GenericDto<UserAdminResultDto>.Error(400, "Foydalanuvchi hali ro'yxatdan to'liq o'tmagan.");

            var (hash, salt) = PasswordHelper.CreatePassword(dto.NewPassword);
            user.PasswordHash = hash;
            user.PasswordSalt = salt;
            await _userRepo.UpdateAsync(user);

            return GenericDto<UserAdminResultDto>.Success(new UserAdminResultDto
            {
                Id = user.Id,
                ResultMessage = "Parol muvaffaqiyatli yangilandi."
            });
        }

        public async Task<GenericDto<PagedResult<UserAdminItemDto>>> GetAllAsync(PaginationParams param)
        {
            var page = await _userRepo.GetAllAsync(param);
            return GenericDto<PagedResult<UserAdminItemDto>>.Success(page.Map(ToItem));
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
            await _userRepo.UpdateAsync(user);

            return GenericDto<UserAdminResultDto>.Success(new UserAdminResultDto
            {
                Id = user.Id,
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
            await _userRepo.UpdateAsync(user);

            return GenericDto<UserAdminResultDto>.Success(new UserAdminResultDto
            {
                Id = user.Id,
                ResultMessage = "Foydalanuvchi blokdan chiqarildi."
            });
        }

        public async Task<GenericDto<UserAdminResultDto>> DeleteAsync(long userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user is null)
                return GenericDto<UserAdminResultDto>.Error(404, "Foydalanuvchi topilmadi.");

            await _userRepo.DeleteAsync(userId);

            return GenericDto<UserAdminResultDto>.Success(new UserAdminResultDto
            {
                Id = userId,
                ResultMessage = "Foydalanuvchi o'chirildi."
            });
        }

        private static UserAdminItemDto ToItem(PlatformUserEntity u) => new()
        {
            Id = u.Id,
            PhoneNumber = u.PhoneNumber,
            Mail = u.Mail,
            SubType = u.Type.ToString(),
            MerchantId = u.MerchantId,
            IsVerified = u.IsVerified,
            IsBlocked = u.IsBlocked,
            RoleId = u.RoleId,
            RoleName = u.Role?.Name,
            CreatedDate = u.CreatedDate,
            LastLoginDate = u.LastLoginDate
        };
    }
}
