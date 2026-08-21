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
    public class CustomerAdminService : ICustomerAdminService
    {
        private readonly ICustomerUserRepository _userRepo;
        private readonly ICustomerRoleRepository _roleRepo;
        private readonly IOrganizationRepository _orgRepo;
        private readonly IPlatformUserRepository _platformUserRepo;
        private readonly IUsageProbeRepository _usageProbe;

        public CustomerAdminService(
            ICustomerUserRepository userRepo,
            ICustomerRoleRepository roleRepo,
            IOrganizationRepository orgRepo,
            IPlatformUserRepository platformUserRepo,
            IUsageProbeRepository usageProbe)
        {
            _userRepo = userRepo;
            _roleRepo = roleRepo;
            _orgRepo = orgRepo;
            _platformUserRepo = platformUserRepo;
            _usageProbe = usageProbe;
        }

        public async Task<GenericDto<UserAdminResultDto>> CreateAsync(CreateCorporateUserDto dto, AccessScope scope)
        {
            var org = await _orgRepo.GetByIdAsync(dto.OrganizationId);
            var role = await _roleRepo.GetByIdAsync(dto.RoleId);

            var stop = await StopFactorCheck.For(StopActions.UserCreate)
                .StopIf(!scope.CanAccessOrganization(dto.OrganizationId), StopFactors.Organization.OutOfScope)
                .StopIfAsync(async () => await _userRepo.GetByPhoneNumberAsync(dto.PhoneNumber) is not null,
                             StopFactors.User.PhoneTaken("foydalanuvchi"))
                .StopIfAsync(() => _userRepo.ExistsByMailAsync(dto.Mail),
                             StopFactors.User.MailTaken("foydalanuvchi"))
                .StopIf(org is null, StopFactors.Organization.NotFound)
                .StopIf(() => !org!.IsActive, StopFactors.Organization.Inactive)
                .StopIf(role is null, StopFactors.Role.NotFound)
                .StopIf(() => role!.OrganizationId != dto.OrganizationId, StopFactors.Role.OrganizationMismatch)
                .ResultAsync();

            if (stop is not null)
                return GenericDto<UserAdminResultDto>.Blocked(stop);

            var newUser = new CustomerUserEntity
            {
                Type = CustomerUserType.Corporate,
                PhoneId = dto.PhoneId,
                PhoneNumber = dto.PhoneNumber,
                Mail = dto.Mail,
                RoleId = dto.RoleId,
                OrganizationId = dto.OrganizationId,
                IsOtpVerified = true,
                IsVerified = false
            };

            var created = await _userRepo.CreateAsync(newUser);

            return GenericDto<UserAdminResultDto>.Success(new UserAdminResultDto
            {
                Id = created.Id,
                ResultMessage = "Corporate foydalanuvchi muvaffaqiyatli yaratildi."
            });
        }

        public async Task<GenericDto<PagedResult<CustomerUserItemDto>>> GetByOrganizationAsync(long organizationId, PaginationParams param, AccessScope scope)
        {
            if (!scope.CanAccessOrganization(organizationId))
                return GenericDto<PagedResult<CustomerUserItemDto>>.Blocked(StopFactors.Organization.OutOfScope);

            // Caller Customer guruhida bo'lsa (corporate bosh-admin), o'zini ro'yxatda ko'rmaydi.
            // Manage (platform) caller uchun ro'yxatga aloqasi yo'q — chiqarilmaydi (ID'lar boshqa jadvalda).
            var excludeUserId = scope.IsCustomer ? scope.UserId : (long?)null;
            var page = await _userRepo.GetByOrganizationAsync(organizationId, param, excludeUserId);
            return GenericDto<PagedResult<CustomerUserItemDto>>.Success(page.Map(ToItem));
        }

        public async Task<GenericDto<PagedResult<CustomerUserItemDto>>> GetNaturalAsync(PaginationParams param, AccessScope scope)
        {
            if (!scope.IsManage)
                return GenericDto<PagedResult<CustomerUserItemDto>>.Error(403, "Jismoniy shaxslar ro'yxatini faqat Manage ko'ra oladi.");

            var page = await _userRepo.GetNaturalAsync(param);
            return GenericDto<PagedResult<CustomerUserItemDto>>.Success(page.Map(ToItem));
        }

        public async Task<GenericDto<CustomerUserItemDto>> GetByIdAsync(long userId, AccessScope scope)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user is null)
                return GenericDto<CustomerUserItemDto>.Blocked(StopFactors.User.NotFound);

            if (!CanManage(user, scope))
                return GenericDto<CustomerUserItemDto>.Blocked(StopFactors.User.OutOfScope);

            return GenericDto<CustomerUserItemDto>.Success(ToItem(user));
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
                .StopIf(() => blocked && userId == scope.UserId, StopFactors.User.SelfAction)
                .StopIf(() => found!.IsBlocked == blocked,
                        blocked ? StopFactors.User.AlreadyBlocked : StopFactors.User.NotBlocked)
                // Bloklash sessiyani to'xtatmaydi: xizmat ketayotgan bo'lsa, avval u yakunlansin —
                // aks holda mijoz bloklangani uchun jarayonni yopa olmay qolardi.
                .StopIfAsync(async () => blocked && await _usageProbe.CustomerUserHasActiveSessionAsync(userId),
                             StopFactors.User.HasActiveSession)
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
                // Yopilmagan sessiyada hisob-kitob tugamagan — foydalanuvchini o'chirsak
                // pul harakati egasiz qolardi.
                .StopIfAsync(() => _usageProbe.CustomerUserHasActiveSessionAsync(userId),
                             StopFactors.User.HasActiveSession)
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
        /// Amalni bajarayotgan admin (caller)ning o'z joriy parolini tekshiradi. Caller Platform
        /// guruhida bo'lsa platform jadvalidan, Customer guruhida bo'lsa customer jadvalidan olinadi.
        /// Muvaffaqiyatli bo'lsa <c>null</c>, aks holda mos xato DTO qaytaradi.
        /// </summary>
        private async Task<GenericDto<UserAdminResultDto>?> VerifyActorPasswordAsync(AccessScope scope, string? currentPassword)
        {
            if (string.IsNullOrWhiteSpace(currentPassword))
                return GenericDto<UserAdminResultDto>.Error(400, "Amalni tasdiqlash uchun o'z joriy parolingizni kiriting.");

            string? hash, salt;
            if (scope.IsPlatform)
            {
                var actor = await _platformUserRepo.GetByIdAsync(scope.UserId);
                (hash, salt) = (actor?.PasswordHash, actor?.PasswordSalt);
            }
            else
            {
                var actor = await _userRepo.GetByIdAsync(scope.UserId);
                (hash, salt) = (actor?.PasswordHash, actor?.PasswordSalt);
            }

            if (hash is null || salt is null)
                return GenericDto<UserAdminResultDto>.Error(403, "Joriy foydalanuvchi parolini tekshirib bo'lmadi.");

            if (!PasswordHelper.Verify(currentPassword, hash, salt))
                return GenericDto<UserAdminResultDto>.Error(403, "Joriy parolingiz noto'g'ri.");

            return null;
        }

        /// <summary>Corporate → tashkilot scope'i; Natural (OrganizationId=null) → faqat Manage.</summary>
        private static bool CanManage(CustomerUserEntity user, AccessScope scope)
            => user.OrganizationId.HasValue
                ? scope.CanAccessOrganization(user.OrganizationId.Value)
                : scope.IsManage;

        private static CustomerUserItemDto ToItem(CustomerUserEntity u) => new()
        {
            Id = u.Id,
            PhoneNumber = u.PhoneNumber,
            Mail = u.Mail,
            SubType = u.Type.ToString(),
            OrganizationId = u.OrganizationId,
            Balance = u.Type == CustomerUserType.Corporate ? (u.Organization?.Balance ?? 0) : u.Balance,
            IsVerified = u.IsVerified,
            IsBlocked = u.IsBlocked,
            RoleId = u.RoleId,
            RoleName = u.Role?.Name,
            CreatedDate = u.CreatedDate,
            LastLoginDate = u.LastLoginDate
        };
    }
}
