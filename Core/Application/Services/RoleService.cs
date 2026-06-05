using Domain.Auth;
using Domain.Constants;
using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    /// <summary>
    /// Platform (Manage/Merchant) rollarini boshqarish.
    /// Manage — barcha platform rollarni; Merchant — faqat o'z merchanti rollarini.
    /// </summary>
    public class RoleService : IRoleService
    {
        private readonly IPlatformRoleRepository _roleRepository;
        private readonly IPermissionRepository _permissionRepository;
        private readonly IMerchantRepository _merchantRepository;

        public RoleService(
            IPlatformRoleRepository roleRepository,
            IPermissionRepository permissionRepository,
            IMerchantRepository merchantRepository)
        {
            _roleRepository = roleRepository;
            _permissionRepository = permissionRepository;
            _merchantRepository = merchantRepository;
        }

        public async Task<GenericDto<CreateRoleResultDto>> CreateRoleAsync(CreateRoleDto dto, AccessScope scope)
        {
            long? merchantId;

            if (scope.IsManage)
            {
                merchantId = dto.MerchantId;
                if (merchantId.HasValue)
                {
                    var merchant = await _merchantRepository.GetByIdAsync(merchantId.Value);
                    if (merchant is null)
                        return GenericDto<CreateRoleResultDto>.Error(404, "Merchant topilmadi.");
                }
            }
            else if (scope.IsMerchant)
            {
                if (scope.MerchantId is null)
                    return GenericDto<CreateRoleResultDto>.Error(403, "Merchant doirasi aniqlanmadi.");
                merchantId = scope.MerchantId;
            }
            else
            {
                return GenericDto<CreateRoleResultDto>.Error(403, "Platform rol yaratish huquqingiz yo'q.");
            }

            var kind = merchantId is null ? RoleKind.PlatformManage : RoleKind.PlatformMerchant;

            var role = new PlatformRoleEntity
            {
                Name = dto.Name,
                Description = dto.Description,
                IsActive = dto.IsActive,
                MerchantId = merchantId
            };

            var permError = await ApplyPermissionsAsync(role, dto.PermissionIds, kind);
            if (permError is not null)
                return GenericDto<CreateRoleResultDto>.Error(permError.Value.code, permError.Value.message);

            var created = await _roleRepository.CreateAsync(role);

            return GenericDto<CreateRoleResultDto>.Success(new CreateRoleResultDto
            {
                ResultMessage = "Rol muvaffaqiyatli yaratildi.",
                RoleId = created.Id
            });
        }

        public async Task<GenericDto<GetRolesResultDto>> GetRolesAsync(AccessScope scope)
        {
            List<PlatformRoleEntity> roles;
            if (scope.IsManage)
                roles = await _roleRepository.GetByScopeAsync(includeManage: true, merchantId: null);
            else if (scope.IsMerchant && scope.MerchantId.HasValue)
                roles = await _roleRepository.GetByScopeAsync(includeManage: false, merchantId: scope.MerchantId);
            else
                return GenericDto<GetRolesResultDto>.Error(403, "Rol ko'rish huquqingiz yo'q.");

            var result = new GetRolesResultDto();
            foreach (var role in roles)
            {
                var permissions = await _roleRepository.GetPermissionsByRoleIdAsync(role.Id);
                result.Roles.Add(ToItem(role, permissions));
            }

            return GenericDto<GetRolesResultDto>.Success(result);
        }

        public async Task<GenericDto<RoleItemDto>> GetRoleByIdAsync(long id, AccessScope scope)
        {
            var role = await _roleRepository.GetByIdAsync(id);
            if (role is null)
                return GenericDto<RoleItemDto>.Error(404, "Rol topilmadi.");

            if (!CanAccess(role, scope))
                return GenericDto<RoleItemDto>.Error(403, "Bu rol sizning doirangizga tegishli emas.");

            var permissions = await _roleRepository.GetPermissionsByRoleIdAsync(role.Id);
            return GenericDto<RoleItemDto>.Success(ToItem(role, permissions));
        }

        public async Task<GenericDto<RoleResultDto>> UpdateRoleAsync(long id, UpdateRoleDto dto, AccessScope scope)
        {
            var role = await _roleRepository.GetByIdWithPermissionsAsync(id);
            if (role is null)
                return GenericDto<RoleResultDto>.Error(404, "Rol topilmadi.");

            if (!CanAccess(role, scope))
                return GenericDto<RoleResultDto>.Error(403, "Bu rol sizning doirangizga tegishli emas.");

            if (!string.IsNullOrWhiteSpace(dto.Name)) role.Name = dto.Name;
            if (dto.Description is not null) role.Description = dto.Description;
            if (dto.IsActive.HasValue) role.IsActive = dto.IsActive.Value;

            if (dto.PermissionIds is not null)
            {
                var kind = role.MerchantId is null ? RoleKind.PlatformManage : RoleKind.PlatformMerchant;
                var newIds = (await _permissionRepository.FilterExistingIdsAsync(dto.PermissionIds)).ToHashSet();
                var allPermissions = await _permissionRepository.GetAllAsync();
                var nameById = allPermissions.ToDictionary(p => p.Id, p => p.Name);

                foreach (var pid in newIds)
                    if (!PermissionScopes.IsAllowedFor(kind, nameById[pid]))
                        return GenericDto<RoleResultDto>.Error(400, $"'{nameById[pid]}' permissioni '{kind}' rolga biriktirilmaydi.");

                role.RolePermissions ??= new List<PlatformRolePermissionEntity>();

                foreach (var rp in role.RolePermissions.Where(rp => !newIds.Contains(rp.PermissionId)))
                    rp.IsDeleted = true;

                var existingIds = role.RolePermissions.Where(rp => !rp.IsDeleted).Select(rp => rp.PermissionId).ToHashSet();
                foreach (var pid in newIds.Except(existingIds))
                    role.RolePermissions.Add(new PlatformRolePermissionEntity { PermissionId = pid });
            }

            await _roleRepository.UpdateAsync(role);

            return GenericDto<RoleResultDto>.Success(new RoleResultDto
            {
                Id = role.Id,
                ResultMessage = "Rol ma'lumotlari yangilandi."
            });
        }

        public async Task<GenericDto<RoleResultDto>> DeleteRoleAsync(long id, AccessScope scope)
        {
            var role = await _roleRepository.GetByIdAsync(id);
            if (role is null)
                return GenericDto<RoleResultDto>.Error(404, "Rol topilmadi.");

            if (!CanAccess(role, scope))
                return GenericDto<RoleResultDto>.Error(403, "Bu rol sizning doirangizga tegishli emas.");

            await _roleRepository.DeleteAsync(id);

            return GenericDto<RoleResultDto>.Success(new RoleResultDto
            {
                Id = id,
                ResultMessage = "Rol o'chirildi."
            });
        }

        public async Task<GenericDto<GetRolePermissionsResultDto>> GetRolePermissionsAsync(long roleId, AccessScope scope)
        {
            var role = await _roleRepository.GetByIdAsync(roleId);
            if (role is null)
                return GenericDto<GetRolePermissionsResultDto>.Error(404, "Rol topilmadi.");

            if (!CanAccess(role, scope))
                return GenericDto<GetRolePermissionsResultDto>.Error(403, "Bu rol sizning doirangizga tegishli emas.");

            var permissions = await _roleRepository.GetPermissionsByRoleIdAsync(roleId);

            return GenericDto<GetRolePermissionsResultDto>.Success(new GetRolePermissionsResultDto
            {
                RoleId = roleId,
                RoleName = role.Name,
                Permissions = permissions
            });
        }

        public async Task<GenericDto<GetAllowedPermissionsResultDto>> GetAllowedPermissionsAsync(RoleKind kind, AccessScope scope)
        {
            if (kind is not (RoleKind.PlatformManage or RoleKind.PlatformMerchant))
                return GenericDto<GetAllowedPermissionsResultDto>.Error(400, "Bu servis faqat platform rol turlarini boshqaradi.");

            if (kind == RoleKind.PlatformManage && !scope.IsManage)
                return GenericDto<GetAllowedPermissionsResultDto>.Error(403, "Manage rol turini boshqarish uchun ruxsatingiz yo'q.");

            var allPermissions = await _permissionRepository.GetAllAsync();
            var allowed = allPermissions
                .Where(p => PermissionScopes.IsAllowedFor(kind, p.Name))
                .Select(p => new AllowedPermissionDto { Id = p.Id, Name = p.Name })
                .ToList();

            return GenericDto<GetAllowedPermissionsResultDto>.Success(new GetAllowedPermissionsResultDto
            {
                Kind = kind,
                Permissions = allowed
            });
        }

        private async Task<(int code, string message)?> ApplyPermissionsAsync(PlatformRoleEntity role, List<long>? permissionIds, RoleKind kind)
        {
            if (permissionIds is null || permissionIds.Count == 0)
                return null;

            var validIds = await _permissionRepository.FilterExistingIdsAsync(permissionIds);
            var permissions = await _permissionRepository.GetAllAsync();
            var nameById = permissions.ToDictionary(p => p.Id, p => p.Name);

            foreach (var pid in validIds)
                if (!PermissionScopes.IsAllowedFor(kind, nameById[pid]))
                    return (400, $"'{nameById[pid]}' permissioni '{kind}' rolga biriktirilmaydi.");

            role.RolePermissions = validIds
                .Select(pid => new PlatformRolePermissionEntity { PermissionId = pid })
                .ToList();

            return null;
        }

        private static bool CanAccess(PlatformRoleEntity role, AccessScope scope)
        {
            if (scope.IsManage)
                return true;
            return scope.IsMerchant && role.MerchantId.HasValue && role.MerchantId == scope.MerchantId;
        }

        private static RoleItemDto ToItem(PlatformRoleEntity role, List<string> permissions) => new()
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            IsActive = role.IsActive,
            Kind = (role.MerchantId is null ? RoleKind.PlatformManage : RoleKind.PlatformMerchant).ToString(),
            MerchantId = role.MerchantId,
            OrganizationId = null,
            Permissions = permissions
        };
    }
}
