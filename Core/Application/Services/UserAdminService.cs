using Application.Helpers;
using Domain.Auth;
using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    /// <summary>
    /// Platform foydalanuvchilarni (Manage/Merchant) boshqarish.
    /// Manage — cheklovsiz; Merchant operator — faqat o'z merchanti operatorlari.
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

        public async Task<GenericDto<UserAdminResultDto>> CreateAsync(CreateUserAdminDto dto, AccessScope scope)
        {
            // Scope: Manage → hammasi; Merchant operator → faqat o'z merchantiga Merchant turi.
            if (!scope.IsManage)
            {
                if (!scope.IsMerchant || scope.MerchantId is null)
                    return GenericDto<UserAdminResultDto>.Error(403, "Platform foydalanuvchi yaratish huquqingiz yo'q.");
                if (dto.Type != PlatformUserType.Merchant)
                    return GenericDto<UserAdminResultDto>.Error(403, "Siz faqat Merchant turidagi operator yarata olasiz.");
                if (dto.MerchantId != scope.MerchantId)
                    return GenericDto<UserAdminResultDto>.Error(403, "Faqat o'z merchantingizga operator qo'sha olasiz.");
            }

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

                if (role.MerchantId != dto.MerchantId.Value)
                    return GenericDto<UserAdminResultDto>.Error(400, "Tanlangan rol ushbu merchantga tegishli bo'lishi kerak.");

                merchantId = dto.MerchantId;
            }
            else
            {
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

        public async Task<GenericDto<PagedResult<UserAdminItemDto>>> GetAllAsync(PaginationParams param, AccessScope scope)
        {
            if (scope.IsManage)
            {
                var all = await _userRepo.GetAllAsync(param);
                return GenericDto<PagedResult<UserAdminItemDto>>.Success(all.Map(ToItem));
            }

            if (scope.IsMerchant && scope.MerchantId.HasValue)
            {
                var page = await _userRepo.GetByMerchantAsync(scope.MerchantId.Value, param);
                return GenericDto<PagedResult<UserAdminItemDto>>.Success(page.Map(ToItem));
            }

            return GenericDto<PagedResult<UserAdminItemDto>>.Success(PagedResult<UserAdminItemDto>.Empty(param));
        }

        public async Task<GenericDto<UserAdminItemDto>> GetByIdAsync(long userId, AccessScope scope)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user is null)
                return GenericDto<UserAdminItemDto>.Error(404, "Foydalanuvchi topilmadi.");
            if (!CanManage(user, scope))
                return GenericDto<UserAdminItemDto>.Error(403, "Bu foydalanuvchi sizning doirangizga tegishli emas.");

            return GenericDto<UserAdminItemDto>.Success(ToItem(user));
        }

        public async Task<GenericDto<UserAdminResultDto>> SetPasswordAsync(SetPasswordAdminDto dto, AccessScope scope)
        {
            var user = await _userRepo.GetByIdAsync(dto.UserId);
            if (user is null)
                return GenericDto<UserAdminResultDto>.Error(404, "Foydalanuvchi topilmadi.");
            if (!CanManage(user, scope))
                return GenericDto<UserAdminResultDto>.Error(403, "Ruxsat yo'q.");

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

        public async Task<GenericDto<UserAdminResultDto>> ResetPasswordAsync(ResetPasswordAdminDto dto, AccessScope scope)
        {
            var user = await _userRepo.GetByIdAsync(dto.UserId);
            if (user is null)
                return GenericDto<UserAdminResultDto>.Error(404, "Foydalanuvchi topilmadi.");
            if (!CanManage(user, scope))
                return GenericDto<UserAdminResultDto>.Error(403, "Ruxsat yo'q.");
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

        public async Task<GenericDto<UserAdminResultDto>> BlockAsync(long userId, AccessScope scope)
            => await SetBlockedAsync(userId, scope, true);

        public async Task<GenericDto<UserAdminResultDto>> UnblockAsync(long userId, AccessScope scope)
            => await SetBlockedAsync(userId, scope, false);

        private async Task<GenericDto<UserAdminResultDto>> SetBlockedAsync(long userId, AccessScope scope, bool blocked)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user is null)
                return GenericDto<UserAdminResultDto>.Error(404, "Foydalanuvchi topilmadi.");
            if (!CanManage(user, scope))
                return GenericDto<UserAdminResultDto>.Error(403, "Ruxsat yo'q.");

            if (user.IsBlocked == blocked)
                return GenericDto<UserAdminResultDto>.Error(400,
                    blocked ? "Foydalanuvchi allaqachon bloklangan." : "Foydalanuvchi bloklanmagan.");

            user.IsBlocked = blocked;
            await _userRepo.UpdateAsync(user);

            return GenericDto<UserAdminResultDto>.Success(new UserAdminResultDto
            {
                Id = user.Id,
                ResultMessage = blocked ? "Foydalanuvchi bloklandi." : "Foydalanuvchi blokdan chiqarildi."
            });
        }

        public async Task<GenericDto<UserAdminResultDto>> DeleteAsync(long userId, AccessScope scope)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user is null)
                return GenericDto<UserAdminResultDto>.Error(404, "Foydalanuvchi topilmadi.");
            if (!CanManage(user, scope))
                return GenericDto<UserAdminResultDto>.Error(403, "Ruxsat yo'q.");

            await _userRepo.DeleteAsync(userId);

            return GenericDto<UserAdminResultDto>.Success(new UserAdminResultDto
            {
                Id = userId,
                ResultMessage = "Foydalanuvchi o'chirildi."
            });
        }

        /// <summary>Manage → har doim; Merchant operator → faqat o'z merchanti operatorlari.</summary>
        private static bool CanManage(PlatformUserEntity target, AccessScope scope)
        {
            if (scope.IsManage)
                return true;
            return scope.IsMerchant
                && target.Type == PlatformUserType.Merchant
                && target.MerchantId.HasValue
                && target.MerchantId == scope.MerchantId;
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
