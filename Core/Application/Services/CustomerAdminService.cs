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
    public class CustomerAdminService : ICustomerAdminService
    {
        private readonly ICustomerUserRepository _userRepo;
        private readonly ICustomerRoleRepository _roleRepo;
        private readonly IOrganizationRepository _orgRepo;

        public CustomerAdminService(
            ICustomerUserRepository userRepo,
            ICustomerRoleRepository roleRepo,
            IOrganizationRepository orgRepo)
        {
            _userRepo = userRepo;
            _roleRepo = roleRepo;
            _orgRepo = orgRepo;
        }

        public async Task<GenericDto<UserAdminResultDto>> CreateAsync(CreateCorporateUserDto dto, AccessScope scope)
        {
            if (!scope.CanAccessOrganization(dto.OrganizationId))
                return GenericDto<UserAdminResultDto>.Error(403, "Bu tashkilot uchun foydalanuvchi yaratish huquqingiz yo'q.");

            var existingUser = await _userRepo.GetByPhoneNumberAsync(dto.PhoneNumber);
            if (existingUser is not null)
                return GenericDto<UserAdminResultDto>.Error(409, "Bu telefon raqam bilan foydalanuvchi allaqachon mavjud.");

            var org = await _orgRepo.GetByIdAsync(dto.OrganizationId);
            if (org is null)
                return GenericDto<UserAdminResultDto>.Error(404, "Tashkilot topilmadi.");
            if (!org.IsActive)
                return GenericDto<UserAdminResultDto>.Error(400, "Tashkilot faol emas.");

            var role = await _roleRepo.GetByIdAsync(dto.RoleId);
            if (role is null)
                return GenericDto<UserAdminResultDto>.Error(404, "Rol topilmadi.");
            if (role.OrganizationId != dto.OrganizationId)
                return GenericDto<UserAdminResultDto>.Error(400, "Tanlangan rol ushbu tashkilotga tegishli bo'lishi kerak.");

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
                return GenericDto<PagedResult<CustomerUserItemDto>>.Error(403, "Bu tashkilot sizning doirangizga tegishli emas.");

            var page = await _userRepo.GetByOrganizationAsync(organizationId, param);
            return GenericDto<PagedResult<CustomerUserItemDto>>.Success(page.Map(ToItem));
        }

        public async Task<GenericDto<CustomerUserItemDto>> GetByIdAsync(long userId, AccessScope scope)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user is null)
                return GenericDto<CustomerUserItemDto>.Error(404, "Foydalanuvchi topilmadi.");

            if (!CanManage(user, scope))
                return GenericDto<CustomerUserItemDto>.Error(403, "Bu foydalanuvchi sizning doirangizga tegishli emas.");

            return GenericDto<CustomerUserItemDto>.Success(ToItem(user));
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

        private static bool CanManage(CustomerUserEntity user, AccessScope scope)
            => user.OrganizationId.HasValue && scope.CanAccessOrganization(user.OrganizationId.Value);

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
