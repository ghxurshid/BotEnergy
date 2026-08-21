using Domain.Helpers;
using Domain.Auth;
using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Enums;
using Domain.Guards;
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
        private readonly IUsageProbeRepository _usageProbe;

        public UserAdminService(
            IPlatformUserRepository userRepo,
            IPlatformRoleRepository roleRepo,
            IMerchantRepository merchantRepo,
            IUsageProbeRepository usageProbe)
        {
            _userRepo = userRepo;
            _roleRepo = roleRepo;
            _merchantRepo = merchantRepo;
            _usageProbe = usageProbe;
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
                return GenericDto<UserAdminResultDto>.Blocked(StopFactors.User.PhoneTaken("platform foydalanuvchi"));

            // mail ustunida ham unique indeks bor — busiz xato faqat INSERT paytida
            // (23505) chiqib, operator sababini bilmay qolardi.
            if (await _userRepo.ExistsByMailAsync(dto.Mail))
                return GenericDto<UserAdminResultDto>.Blocked(StopFactors.User.MailTaken("platform foydalanuvchi"));

            var role = await _roleRepo.GetByIdAsync(dto.RoleId);
            if (role is null)
                return GenericDto<UserAdminResultDto>.Blocked(StopFactors.Role.NotFound);

            long? merchantId = null;

            if (dto.Type == PlatformUserType.Merchant)
            {
                if (dto.MerchantId is null)
                    return GenericDto<UserAdminResultDto>.Error(400, "Merchant foydalanuvchi uchun MerchantId majburiy.");

                var merchant = await _merchantRepo.GetByIdAsync(dto.MerchantId.Value);
                if (merchant is null)
                    return GenericDto<UserAdminResultDto>.Blocked(StopFactors.Merchant.NotFound);
                if (!merchant.IsActive)
                    return GenericDto<UserAdminResultDto>.Blocked(StopFactors.Merchant.Inactive);

                if (role.MerchantId != dto.MerchantId.Value)
                    return GenericDto<UserAdminResultDto>.Blocked(StopFactors.Role.MerchantMismatch);

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
            // Caller o'zini ro'yxatda ko'rmaydi — o'z profilini alohida (/api/Profile) boshqaradi.
            if (scope.IsManage)
            {
                var all = await _userRepo.GetAllAsync(param, excludeUserId: scope.UserId);
                return GenericDto<PagedResult<UserAdminItemDto>>.Success(all.Map(ToItem));
            }

            if (scope.IsMerchant && scope.MerchantId.HasValue)
            {
                var page = await _userRepo.GetByMerchantAsync(scope.MerchantId.Value, param, excludeUserId: scope.UserId);
                return GenericDto<PagedResult<UserAdminItemDto>>.Success(page.Map(ToItem));
            }

            return GenericDto<PagedResult<UserAdminItemDto>>.Success(PagedResult<UserAdminItemDto>.Empty(param));
        }

        public async Task<GenericDto<UserAdminItemDto>> GetByIdAsync(long userId, AccessScope scope)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user is null)
                return GenericDto<UserAdminItemDto>.Blocked(StopFactors.User.NotFound);
            if (!CanManage(user, scope))
                return GenericDto<UserAdminItemDto>.Blocked(StopFactors.User.OutOfScope);

            return GenericDto<UserAdminItemDto>.Success(ToItem(user));
        }

        public async Task<GenericDto<UserAdminResultDto>> SetPasswordAsync(SetPasswordAdminDto dto, AccessScope scope)
        {
            var user = await _userRepo.GetByIdAsync(dto.UserId);
            if (user is null)
                return GenericDto<UserAdminResultDto>.Blocked(StopFactors.User.NotFound);
            if (!CanManage(user, scope))
                return GenericDto<UserAdminResultDto>.Blocked(StopFactors.User.OutOfScope);

            // Boshqa userga parol o'rnatishdan oldin admin o'z joriy parolini tasdiqlaydi.
            var actorCheck = await VerifyActorPasswordAsync(scope, dto.CurrentPassword);
            if (actorCheck is not null)
                return actorCheck;

            if (user.IsVerified)
                return GenericDto<UserAdminResultDto>.Blocked(StopFactors.User.PasswordAlreadySet);
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
                return GenericDto<UserAdminResultDto>.Blocked(StopFactors.User.NotFound);
            if (!CanManage(user, scope))
                return GenericDto<UserAdminResultDto>.Blocked(StopFactors.User.OutOfScope);

            // Boshqa userning parolini reset qilishdan oldin admin o'z joriy parolini tasdiqlaydi.
            var actorCheck = await VerifyActorPasswordAsync(scope, dto.CurrentPassword);
            if (actorCheck is not null)
                return actorCheck;

            if (!user.IsVerified)
                return GenericDto<UserAdminResultDto>.Blocked(StopFactors.User.RegistrationIncomplete);

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
            var found = await _userRepo.GetByIdAsync(userId);

            var stop = await StopFactorCheck.For(blocked ? StopActions.UserBlock : StopActions.UserUnblock)
                .StopIf(found is null, StopFactors.User.NotFound)
                .StopIf(() => !CanManage(found!, scope), StopFactors.User.OutOfScope)
                // O'zini bloklab qo'ygan admin tizimga qayta kira olmaydi.
                .StopIf(() => blocked && userId == scope.UserId, StopFactors.User.SelfAction)
                .StopIf(() => found!.IsBlocked == blocked,
                        blocked ? StopFactors.User.AlreadyBlocked : StopFactors.User.NotBlocked)
                // Oxirgi Manage admin bloklansa, platformani boshqaradigan hech kim qolmaydi.
                .StopIfAsync(async () => blocked
                                         && found!.Type == PlatformUserType.Manage
                                         && await _usageProbe.ActiveManageUserCountAsync(userId) == 0,
                             StopFactors.User.LastManage)
                .ResultAsync();

            if (stop is not null)
                return GenericDto<UserAdminResultDto>.Blocked(stop);

            var user = found!;
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

            var stop = await StopFactorCheck.For(StopActions.UserDelete)
                .StopIf(user is null, StopFactors.User.NotFound)
                .StopIf(() => !CanManage(user!, scope), StopFactors.User.OutOfScope)
                .StopIf(userId == scope.UserId, StopFactors.User.SelfAction)
                // Oxirgi Manage admin o'chirilsa platforma boshqaruvsiz qoladi.
                .StopIfAsync(async () => user!.Type == PlatformUserType.Manage
                                         && await _usageProbe.ActiveManageUserCountAsync(userId) == 0,
                             StopFactors.User.LastManage)
                .ResultAsync();

            if (stop is not null)
                return GenericDto<UserAdminResultDto>.Blocked(stop);

            await _userRepo.DeleteAsync(userId);

            return GenericDto<UserAdminResultDto>.Success(new UserAdminResultDto
            {
                Id = userId,
                ResultMessage = "Foydalanuvchi o'chirildi."
            });
        }

        /// <summary>
        /// Amalni bajarayotgan admin (caller)ning o'z joriy parolini tekshiradi.
        /// Muvaffaqiyatli bo'lsa <c>null</c>, aks holda mos xato DTO qaytaradi.
        /// </summary>
        private async Task<GenericDto<UserAdminResultDto>?> VerifyActorPasswordAsync(AccessScope scope, string? currentPassword)
        {
            if (string.IsNullOrWhiteSpace(currentPassword))
                return GenericDto<UserAdminResultDto>.Error(400, "Amalni tasdiqlash uchun o'z joriy parolingizni kiriting.");

            var actor = await _userRepo.GetByIdAsync(scope.UserId);
            if (actor?.PasswordHash is null || actor.PasswordSalt is null)
                return GenericDto<UserAdminResultDto>.Error(403, "Joriy foydalanuvchi parolini tekshirib bo'lmadi.");

            if (!PasswordHelper.Verify(currentPassword, actor.PasswordHash, actor.PasswordSalt))
                return GenericDto<UserAdminResultDto>.Error(403, "Joriy parolingiz noto'g'ri.");

            return null;
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
