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
    /// Customer (Corporate) rollarini boshqarish — tashkilot scope ichida.
    /// Manage — istalgan tashkilot rollarini; Corporate admin — faqat o'z tashkiloti rollarini.
    /// </summary>
    public class CustomerRoleService : ICustomerRoleService
    {
        private readonly ICustomerRoleRepository _roleRepository;
        private readonly IPermissionRepository _permissionRepository;
        private readonly IOrganizationRepository _organizationRepository;

        public CustomerRoleService(
            ICustomerRoleRepository roleRepository,
            IPermissionRepository permissionRepository,
            IOrganizationRepository organizationRepository)
        {
            _roleRepository = roleRepository;
            _permissionRepository = permissionRepository;
            _organizationRepository = organizationRepository;
        }

        public async Task<GenericDto<CreateRoleResultDto>> CreateRoleAsync(CreateRoleDto dto, AccessScope scope)
        {
            long organizationId;

            if (scope.IsManage)
            {
                if (dto.OrganizationId is null)
                    return GenericDto<CreateRoleResultDto>.Error(400, "Corporate rol uchun OrganizationId majburiy.");
                organizationId = dto.OrganizationId.Value;
            }
            else if (scope.IsCorporate && scope.OrganizationId.HasValue)
            {
                organizationId = scope.OrganizationId.Value;
            }
            else
            {
                return GenericDto<CreateRoleResultDto>.Error(403, "Corporate rol yaratish huquqingiz yo'q.");
            }

            var org = await _organizationRepository.GetByIdAsync(organizationId);
            if (org is null)
                return GenericDto<CreateRoleResultDto>.Error(404, "Tashkilot topilmadi.");

            var role = new CustomerRoleEntity
            {
                Name = dto.Name,
                Description = dto.Description,
                IsActive = dto.IsActive,
                OrganizationId = organizationId
            };

            var permError = await ApplyPermissionsAsync(role, dto.PermissionIds);
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
            List<CustomerRoleEntity> roles;
            if (scope.IsManage)
                roles = await _roleRepository.GetByScopeAsync(includeNatural: true, organizationId: null);
            else if (scope.IsCorporate && scope.OrganizationId.HasValue)
                roles = await _roleRepository.GetByScopeAsync(includeNatural: false, organizationId: scope.OrganizationId);
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
                var newIds = (await _permissionRepository.FilterExistingIdsAsync(dto.PermissionIds)).ToHashSet();
                var allPermissions = await _permissionRepository.GetAllAsync();
                var nameById = allPermissions.ToDictionary(p => p.Id, p => p.Name);

                foreach (var pid in newIds)
                    if (!PermissionScopes.IsAllowedFor(RoleKind.CustomerCorporate, nameById[pid]))
                        return GenericDto<RoleResultDto>.Error(400, $"'{nameById[pid]}' permissioni corporate rolga biriktirilmaydi.");

                role.RolePermissions ??= new List<CustomerRolePermissionEntity>();

                foreach (var rp in role.RolePermissions.Where(rp => !newIds.Contains(rp.PermissionId)))
                    rp.IsDeleted = true;

                var existingIds = role.RolePermissions.Where(rp => !rp.IsDeleted).Select(rp => rp.PermissionId).ToHashSet();
                foreach (var pid in newIds.Except(existingIds))
                    role.RolePermissions.Add(new CustomerRolePermissionEntity { PermissionId = pid });
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

        public async Task<GenericDto<GetAllowedPermissionsResultDto>> GetAllowedPermissionsAsync()
        {
            var allPermissions = await _permissionRepository.GetAllAsync();
            var allowed = allPermissions
                .Where(p => PermissionScopes.IsAllowedFor(RoleKind.CustomerCorporate, p.Name))
                .Select(p => new AllowedPermissionDto { Id = p.Id, Name = p.Name })
                .ToList();

            return GenericDto<GetAllowedPermissionsResultDto>.Success(new GetAllowedPermissionsResultDto
            {
                Kind = RoleKind.CustomerCorporate,
                Permissions = allowed
            });
        }

        private async Task<(int code, string message)?> ApplyPermissionsAsync(CustomerRoleEntity role, List<long>? permissionIds)
        {
            if (permissionIds is null || permissionIds.Count == 0)
                return null;

            var validIds = await _permissionRepository.FilterExistingIdsAsync(permissionIds);
            var permissions = await _permissionRepository.GetAllAsync();
            var nameById = permissions.ToDictionary(p => p.Id, p => p.Name);

            foreach (var pid in validIds)
                if (!PermissionScopes.IsAllowedFor(RoleKind.CustomerCorporate, nameById[pid]))
                    return (400, $"'{nameById[pid]}' permissioni corporate rolga biriktirilmaydi.");

            role.RolePermissions = validIds
                .Select(pid => new CustomerRolePermissionEntity { PermissionId = pid })
                .ToList();

            return null;
        }

        private static bool CanAccess(CustomerRoleEntity role, AccessScope scope)
        {
            if (scope.IsManage)
                return true;
            return scope.IsCorporate && role.OrganizationId.HasValue && role.OrganizationId == scope.OrganizationId;
        }

        private static RoleItemDto ToItem(CustomerRoleEntity role, List<string> permissions) => new()
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            IsActive = role.IsActive,
            Kind = (role.OrganizationId is null ? RoleKind.CustomerNatural : RoleKind.CustomerCorporate).ToString(),
            MerchantId = null,
            OrganizationId = role.OrganizationId,
            Permissions = permissions
        };
    }
}
