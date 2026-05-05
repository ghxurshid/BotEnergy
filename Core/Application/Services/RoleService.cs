using Domain.Constants;
using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    public class RoleService : IRoleService
    {
        private readonly IRoleRepository _roleRepository;
        private readonly IUserRepository _userRepository;
        private readonly IStationRepository _stationRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IMerchantRepository _merchantRepository;

        public RoleService(
            IRoleRepository roleRepository,
            IUserRepository userRepository,
            IStationRepository stationRepository,
            IOrganizationRepository organizationRepository,
            IMerchantRepository merchantRepository)
        {
            _roleRepository = roleRepository;
            _userRepository = userRepository;
            _stationRepository = stationRepository;
            _organizationRepository = organizationRepository;
            _merchantRepository = merchantRepository;
        }

        public async Task<GenericDto<CreateRoleResultDto>> CreateRoleAsync(CreateRoleDto dto, long callerId, HashSet<string> callerPermissions)
        {
            var caller = await _userRepository.GetByIdAsync(callerId);
            if (caller is null)
                return GenericDto<CreateRoleResultDto>.Error(401, "Foydalanuvchi aniqlanmadi.");

            // Organization OrganizationId ustuvor — UserAdminService dagi qoidaga mos.
            if (dto.OrganizationId.HasValue && dto.StationId.HasValue)
                dto.StationId = null;

            RoleEntity role;

            if (dto.OrganizationId.HasValue)
            {
                var orgCheck = await EnsureCanCreateInOrganizationAsync(caller, callerPermissions, dto.OrganizationId.Value);
                if (orgCheck is not null)
                    return GenericDto<CreateRoleResultDto>.Error(orgCheck.Code, orgCheck.Message);

                role = new LegalRoleEntity
                {
                    Name = dto.Name,
                    Description = dto.Description,
                    IsActive = dto.IsActive,
                    OrganizationId = dto.OrganizationId.Value
                };
            }
            else if (dto.StationId.HasValue)
            {
                var station = await _stationRepository.GetByIdAsync(dto.StationId.Value);
                if (station is null)
                    return GenericDto<CreateRoleResultDto>.Error(404, "Stansiya topilmadi.");

                var stationCheck = await EnsureCanCreateInMerchantAsync(caller, callerPermissions, station.MerchantId);
                if (stationCheck is not null)
                    return GenericDto<CreateRoleResultDto>.Error(stationCheck.Code, stationCheck.Message);

                role = new MerchantRoleEntity
                {
                    Name = dto.Name,
                    Description = dto.Description,
                    IsActive = dto.IsActive,
                    MerchantId = station.MerchantId
                };
            }
            else
            {
                if (caller is not NaturalUserEntity)
                    return GenericDto<CreateRoleResultDto>.Error(403,
                        "Global rol yaratish uchun tegishli scopega biriktirilmagan foydalanuvchi bo'lishingiz kerak.");

                role = new NaturalRoleEntity
                {
                    Name = dto.Name,
                    Description = dto.Description,
                    IsActive = dto.IsActive
                };
            }

            if (dto.PermissionIds is not null && dto.PermissionIds.Count > 0)
            {
                var validIds = await _roleRepository.FilterExistingPermissionIdsAsync(dto.PermissionIds);
                var permissions = await _roleRepository.GetAllPermissionsAsync();
                var nameById = permissions.ToDictionary(p => p.Id, p => p.Name);

                foreach (var pid in validIds)
                {
                    if (!PermissionScopes.IsAllowedFor(role.RoleType, nameById[pid]))
                        return GenericDto<CreateRoleResultDto>.Error(400,
                            $"'{nameById[pid]}' permissioni '{role.RoleType}' rolga biriktirilmaydi.");
                }

                role.RolePermissions = validIds
                    .Select(pid => new RolePermissionEntity { PermissionId = pid })
                    .ToList();
            }

            var created = await _roleRepository.CreateAsync(role);

            return GenericDto<CreateRoleResultDto>.Success(new CreateRoleResultDto
            {
                ResultMessage = "Rol muvaffaqiyatli yaratildi.",
                RoleId = created.Id
            });
        }

        public async Task<GenericDto<GetRolesResultDto>> GetRolesAsync(long callerId, HashSet<string> callerPermissions)
        {
            var caller = await _userRepository.GetByIdAsync(callerId);
            if (caller is null)
                return GenericDto<GetRolesResultDto>.Error(401, "Foydalanuvchi aniqlanmadi.");

            var scope = await BuildAccessibleScopeAsync(caller, callerPermissions);

            var roles = await _roleRepository.GetByScopeAsync(scope.RoleTypes, scope.OrganizationId, scope.MerchantId);

            var result = new GetRolesResultDto();
            foreach (var role in roles)
            {
                var permissions = await _roleRepository.GetPermissionsByRoleIdAsync(role.Id);
                result.Roles.Add(ToItem(role, permissions));
            }

            return GenericDto<GetRolesResultDto>.Success(result);
        }

        public async Task<GenericDto<RoleItemDto>> GetRoleByIdAsync(long id, long callerId, HashSet<string> callerPermissions)
        {
            var caller = await _userRepository.GetByIdAsync(callerId);
            if (caller is null)
                return GenericDto<RoleItemDto>.Error(401, "Foydalanuvchi aniqlanmadi.");

            var role = await _roleRepository.GetByIdAsync(id);
            if (role is null)
                return GenericDto<RoleItemDto>.Error(404, "Rol topilmadi.");

            var accessCheck = await EnsureCanAccessRoleAsync(caller, callerPermissions, role);
            if (accessCheck is not null)
                return GenericDto<RoleItemDto>.Error(accessCheck.Code, accessCheck.Message);

            var permissions = await _roleRepository.GetPermissionsByRoleIdAsync(role.Id);
            return GenericDto<RoleItemDto>.Success(ToItem(role, permissions));
        }

        public async Task<GenericDto<RoleResultDto>> UpdateRoleAsync(long id, UpdateRoleDto dto, long callerId, HashSet<string> callerPermissions)
        {
            var caller = await _userRepository.GetByIdAsync(callerId);
            if (caller is null)
                return GenericDto<RoleResultDto>.Error(401, "Foydalanuvchi aniqlanmadi.");

            var role = await _roleRepository.GetByIdWithPermissionsAsync(id);
            if (role is null)
                return GenericDto<RoleResultDto>.Error(404, "Rol topilmadi.");

            var accessCheck = await EnsureCanAccessRoleAsync(caller, callerPermissions, role);
            if (accessCheck is not null)
                return GenericDto<RoleResultDto>.Error(accessCheck.Code, accessCheck.Message);

            // Scope (RoleType, MerchantId, OrganizationId) o'zgartirilmaydi —
            // faqat name, description, isActive va permissionlar.
            if (!string.IsNullOrWhiteSpace(dto.Name)) role.Name = dto.Name;
            if (dto.Description is not null) role.Description = dto.Description;
            if (dto.IsActive.HasValue) role.IsActive = dto.IsActive.Value;

            if (dto.PermissionIds is not null)
            {
                var newIds = (await _roleRepository.FilterExistingPermissionIdsAsync(dto.PermissionIds)).ToHashSet();
                var allPermissions = await _roleRepository.GetAllPermissionsAsync();
                var nameById = allPermissions.ToDictionary(p => p.Id, p => p.Name);

                foreach (var pid in newIds)
                {
                    if (!PermissionScopes.IsAllowedFor(role.RoleType, nameById[pid]))
                        return GenericDto<RoleResultDto>.Error(400,
                            $"'{nameById[pid]}' permissioni '{role.RoleType}' rolga biriktirilmaydi.");
                }

                role.RolePermissions ??= new List<RolePermissionEntity>();

                foreach (var rp in role.RolePermissions.Where(rp => !newIds.Contains(rp.PermissionId)))
                    rp.IsDeleted = true;

                var existingIds = role.RolePermissions
                    .Where(rp => !rp.IsDeleted)
                    .Select(rp => rp.PermissionId)
                    .ToHashSet();

                foreach (var pid in newIds.Except(existingIds))
                    role.RolePermissions.Add(new RolePermissionEntity { PermissionId = pid });
            }

            await _roleRepository.UpdateAsync(role);

            return GenericDto<RoleResultDto>.Success(new RoleResultDto
            {
                Id = role.Id,
                ResultMessage = "Rol ma'lumotlari yangilandi."
            });
        }

        public async Task<GenericDto<RoleResultDto>> DeleteRoleAsync(long id, long callerId, HashSet<string> callerPermissions)
        {
            var caller = await _userRepository.GetByIdAsync(callerId);
            if (caller is null)
                return GenericDto<RoleResultDto>.Error(401, "Foydalanuvchi aniqlanmadi.");

            var role = await _roleRepository.GetByIdAsync(id);
            if (role is null)
                return GenericDto<RoleResultDto>.Error(404, "Rol topilmadi.");

            var accessCheck = await EnsureCanAccessRoleAsync(caller, callerPermissions, role);
            if (accessCheck is not null)
                return GenericDto<RoleResultDto>.Error(accessCheck.Code, accessCheck.Message);

            await _roleRepository.DeleteAsync(id);

            return GenericDto<RoleResultDto>.Success(new RoleResultDto
            {
                Id = id,
                ResultMessage = "Rol o'chirildi."
            });
        }

        public async Task<GenericDto<GetRolePermissionsResultDto>> GetRolePermissionsAsync(long roleId, long callerId, HashSet<string> callerPermissions)
        {
            var caller = await _userRepository.GetByIdAsync(callerId);
            if (caller is null)
                return GenericDto<GetRolePermissionsResultDto>.Error(401, "Foydalanuvchi aniqlanmadi.");

            var role = await _roleRepository.GetByIdAsync(roleId);
            if (role is null)
                return GenericDto<GetRolePermissionsResultDto>.Error(404, "Rol topilmadi.");

            var accessCheck = await EnsureCanAccessRoleAsync(caller, callerPermissions, role);
            if (accessCheck is not null)
                return GenericDto<GetRolePermissionsResultDto>.Error(accessCheck.Code, accessCheck.Message);

            var permissions = await _roleRepository.GetPermissionsByRoleIdAsync(roleId);

            return GenericDto<GetRolePermissionsResultDto>.Success(new GetRolePermissionsResultDto
            {
                RoleId = roleId,
                RoleName = role.Name,
                Permissions = permissions
            });
        }

        public async Task<GenericDto<GetAllowedPermissionsResultDto>> GetAllowedPermissionsAsync(RoleType roleType, long callerId, HashSet<string> callerPermissions)
        {
            var caller = await _userRepository.GetByIdAsync(callerId);
            if (caller is null)
                return GenericDto<GetAllowedPermissionsResultDto>.Error(401, "Foydalanuvchi aniqlanmadi.");

            var managementCheck = EnsureCanManageRoleType(caller, callerPermissions, roleType);
            if (managementCheck is not null)
                return GenericDto<GetAllowedPermissionsResultDto>.Error(managementCheck.Code, managementCheck.Message);

            var allPermissions = await _roleRepository.GetAllPermissionsAsync();
            var allowed = allPermissions
                .Where(p => PermissionScopes.IsAllowedFor(roleType, p.Name))
                .Select(p => new AllowedPermissionDto { Id = p.Id, Name = p.Name })
                .ToList();

            return GenericDto<GetAllowedPermissionsResultDto>.Success(new GetAllowedPermissionsResultDto
            {
                RoleType = roleType,
                Permissions = allowed
            });
        }

        // ── Permission flow helpers ────────────────────────────────────────────

        /// <summary>
        /// Caller qaysi scopelardagi rollarni ko'ra olishini hisoblaydi.
        /// </summary>
        private async Task<AccessibleScope> BuildAccessibleScopeAsync(UserEntity caller, HashSet<string> callerPermissions)
        {
            switch (caller)
            {
                case LegalUserEntity legal:
                    return new AccessibleScope(
                        RoleTypes: new[] { RoleType.LegalRole },
                        OrganizationId: legal.OrganizationId,
                        MerchantId: null);

                case MerchantUserEntity merchantUser:
                    var station = merchantUser.Station
                        ?? await _stationRepository.GetByIdAsync(merchantUser.StationId);
                    return new AccessibleScope(
                        RoleTypes: new[] { RoleType.MerchantRole },
                        OrganizationId: null,
                        MerchantId: station?.MerchantId);

                default:
                    // NaturalUser (global) — qaysi scopelarga accessi borligiga qarab tasniflanadi.
                    var types = new List<RoleType> { RoleType.NaturalRole };
                    if (callerPermissions.Contains(Permissions.OrganizationAdminGetAll))
                        types.Add(RoleType.LegalRole);
                    if (callerPermissions.Contains(Permissions.MerchantAdminGetAll))
                        types.Add(RoleType.MerchantRole);

                    return new AccessibleScope(
                        RoleTypes: types,
                        OrganizationId: null,
                        MerchantId: null);
            }
        }

        /// <summary>
        /// Caller berilgan rolga (uning scopega) kira olishini tekshiradi.
        /// </summary>
        private async Task<AccessError?> EnsureCanAccessRoleAsync(UserEntity caller, HashSet<string> callerPermissions, RoleEntity role)
        {
            switch (caller)
            {
                case LegalUserEntity legal:
                    if (role is not LegalRoleEntity legalRole)
                        return new AccessError(403, "Tashkilot foydalanuvchisi faqat o'z tashkiloti rollarini boshqara oladi.");
                    if (legalRole.OrganizationId != legal.OrganizationId)
                        return new AccessError(403, "Bu rol sizning tashkilotingizga tegishli emas.");
                    return null;

                case MerchantUserEntity merchantUser:
                    if (role is not MerchantRoleEntity merchantRole)
                        return new AccessError(403, "Merchant foydalanuvchisi faqat o'z merchanti rollarini boshqara oladi.");
                    var station = merchantUser.Station
                        ?? await _stationRepository.GetByIdAsync(merchantUser.StationId);
                    if (station is null || merchantRole.MerchantId != station.MerchantId)
                        return new AccessError(403, "Bu rol sizning merchantingizga tegishli emas.");
                    return null;

                default:
                    // NaturalUser (global): cross-scope kirish uchun tegishli permission kerak.
                    return role switch
                    {
                        LegalRoleEntity when !callerPermissions.Contains(Permissions.OrganizationAdminGetById)
                            => new AccessError(403, "Tashkilot rollari bilan ishlash uchun 'organization.admin.getbyid' ruxsati kerak."),
                        MerchantRoleEntity when !callerPermissions.Contains(Permissions.MerchantAdminGetById)
                            => new AccessError(403, "Merchant rollari bilan ishlash uchun 'merchant.admin.getbyid' ruxsati kerak."),
                        _ => null
                    };
            }
        }

        /// <summary>
        /// Caller berilgan organization scopeida rol yarata olishini tekshiradi.
        /// </summary>
        private async Task<AccessError?> EnsureCanCreateInOrganizationAsync(UserEntity caller, HashSet<string> callerPermissions, long organizationId)
        {
            var organization = await _organizationRepository.GetByIdAsync(organizationId);
            if (organization is null)
                return new AccessError(404, "Tashkilot topilmadi.");

            if (!organization.IsActive)
                return new AccessError(400, "Tashkilot faol emas.");

            switch (caller)
            {
                case LegalUserEntity legal:
                    if (legal.OrganizationId != organizationId)
                        return new AccessError(403, "Faqat o'z tashkilotingiz uchun rol yarata olasiz.");
                    return null;

                case MerchantUserEntity:
                    return new AccessError(403, "Merchant foydalanuvchisi tashkilot rolini yarata olmaydi.");

                default:
                    if (!callerPermissions.Contains(Permissions.OrganizationAdminCreate))
                        return new AccessError(403, "Tashkilot scopeida rol yaratish uchun 'organization.admin.create' ruxsati kerak.");
                    return null;
            }
        }

        /// <summary>
        /// Caller berilgan merchant scopeida rol yarata olishini tekshiradi.
        /// </summary>
        private async Task<AccessError?> EnsureCanCreateInMerchantAsync(UserEntity caller, HashSet<string> callerPermissions, long merchantId)
        {
            var merchant = await _merchantRepository.GetByIdAsync(merchantId);
            if (merchant is null)
                return new AccessError(404, "Merchant topilmadi.");

            if (!merchant.IsActive)
                return new AccessError(400, "Merchant faol emas.");

            switch (caller)
            {
                case MerchantUserEntity merchantUser:
                    var station = merchantUser.Station
                        ?? await _stationRepository.GetByIdAsync(merchantUser.StationId);
                    if (station is null || station.MerchantId != merchantId)
                        return new AccessError(403, "Faqat o'z merchantingiz uchun rol yarata olasiz.");
                    return null;

                case LegalUserEntity:
                    return new AccessError(403, "Tashkilot foydalanuvchisi merchant rolini yarata olmaydi.");

                default:
                    if (!callerPermissions.Contains(Permissions.MerchantAdminRegister) ||
                        !callerPermissions.Contains(Permissions.StationAdminCreate))
                        return new AccessError(403,
                            "Merchant scopeida rol yaratish uchun 'merchant.admin.register' va 'station.admin.create' ruxsatlari kerak.");
                    return null;
            }
        }

        /// <summary>
        /// Caller berilgan rol turini boshqara olishini tekshiradi
        /// (GetAllowedPermissions uchun).
        /// </summary>
        private static AccessError? EnsureCanManageRoleType(UserEntity caller, HashSet<string> callerPermissions, RoleType roleType)
            => (caller, roleType) switch
            {
                (LegalUserEntity, RoleType.LegalRole) => null,
                (LegalUserEntity, _) => new AccessError(403,
                    "Tashkilot foydalanuvchisi faqat tashkilot rolini boshqara oladi."),

                (MerchantUserEntity, RoleType.MerchantRole) => null,
                (MerchantUserEntity, _) => new AccessError(403,
                    "Merchant foydalanuvchisi faqat merchant rolini boshqara oladi."),

                (_, RoleType.NaturalRole) => null,

                (_, RoleType.LegalRole) when !callerPermissions.Contains(Permissions.OrganizationAdminCreate)
                    => new AccessError(403, "Tashkilot rolini boshqarish uchun 'organization.admin.create' ruxsati kerak."),
                (_, RoleType.LegalRole) => null,

                (_, RoleType.MerchantRole) when
                    !callerPermissions.Contains(Permissions.MerchantAdminRegister) ||
                    !callerPermissions.Contains(Permissions.StationAdminCreate)
                    => new AccessError(403,
                        "Merchant rolini boshqarish uchun 'merchant.admin.register' va 'station.admin.create' ruxsatlari kerak."),
                (_, RoleType.MerchantRole) => null,

                _ => null
            };

        private static RoleItemDto ToItem(RoleEntity role, List<string> permissions) => new()
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            IsActive = role.IsActive,
            RoleType = role.RoleType,
            OrganizationId = (role as LegalRoleEntity)?.OrganizationId,
            MerchantId = (role as MerchantRoleEntity)?.MerchantId,
            Permissions = permissions
        };

        private sealed record AccessibleScope(
            IReadOnlyCollection<RoleType> RoleTypes,
            long? OrganizationId,
            long? MerchantId);

        private sealed record AccessError(int Code, string Message);
    }
}
