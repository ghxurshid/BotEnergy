using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    public class RoleService : IRoleService
    {
        private readonly IRoleRepository _roleRepository;

        public RoleService(IRoleRepository roleRepository)
        {
            _roleRepository = roleRepository;
        }

        public async Task<GenericDto<CreateRoleResultDto>> CreateRoleAsync(CreateRoleDto dto)
        {
            RoleEntity role = new NaturalRoleEntity
            {
                Name = dto.Name,
                Description = dto.Description,
                IsActive = dto.IsActive
            };

            if (dto.PermissionIds is not null && dto.PermissionIds.Count > 0)
            {
                var validIds = await _roleRepository.FilterExistingPermissionIdsAsync(dto.PermissionIds);
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

        public async Task<GenericDto<GetRolesResultDto>> GetRolesAsync()
        {
            var roles = await _roleRepository.GetAllAsync();
            var result = new GetRolesResultDto();

            foreach (var role in roles)
            {
                var permissions = await _roleRepository.GetPermissionsByRoleIdAsync(role.Id);
                result.Roles.Add(new RoleItemDto
                {
                    Id = role.Id,
                    Name = role.Name,
                    Description = role.Description,
                    IsActive = role.IsActive,
                    Permissions = permissions
                });
            }

            return GenericDto<GetRolesResultDto>.Success(result);
        }

        public async Task<GenericDto<RoleItemDto>> GetRoleByIdAsync(long id)
        {
            var role = await _roleRepository.GetByIdAsync(id);
            if (role is null)
                return GenericDto<RoleItemDto>.Error(404, "Rol topilmadi.");

            var permissions = await _roleRepository.GetPermissionsByRoleIdAsync(role.Id);

            return GenericDto<RoleItemDto>.Success(new RoleItemDto
            {
                Id = role.Id,
                Name = role.Name,
                Description = role.Description,
                IsActive = role.IsActive,
                Permissions = permissions
            });
        }

        public async Task<GenericDto<RoleResultDto>> UpdateRoleAsync(long id, UpdateRoleDto dto)
        {
            var role = await _roleRepository.GetByIdWithPermissionsAsync(id);
            if (role is null)
                return GenericDto<RoleResultDto>.Error(404, "Rol topilmadi.");

            if (!string.IsNullOrWhiteSpace(dto.Name)) role.Name = dto.Name;
            if (dto.Description is not null) role.Description = dto.Description;
            if (dto.IsActive.HasValue) role.IsActive = dto.IsActive.Value;

            if (dto.PermissionIds is not null)
            {
                var newIds = (await _roleRepository.FilterExistingPermissionIdsAsync(dto.PermissionIds)).ToHashSet();
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

        public async Task<GenericDto<RoleResultDto>> DeleteRoleAsync(long id)
        {
            var role = await _roleRepository.GetByIdAsync(id);
            if (role is null)
                return GenericDto<RoleResultDto>.Error(404, "Rol topilmadi.");

            await _roleRepository.DeleteAsync(id);

            return GenericDto<RoleResultDto>.Success(new RoleResultDto
            {
                Id = id,
                ResultMessage = "Rol o'chirildi."
            });
        }

        public async Task<GenericDto<GetRolePermissionsResultDto>> GetRolePermissionsAsync(long roleId)
        {
            var role = await _roleRepository.GetByIdAsync(roleId);
            if (role is null)
                return GenericDto<GetRolePermissionsResultDto>.Error(404, "Rol topilmadi.");

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
            var permissions = await _roleRepository.GetAllPermissionsAsync();

            return GenericDto<GetAllowedPermissionsResultDto>.Success(new GetAllowedPermissionsResultDto
            {
                Permissions = permissions
                    .Select(p => new AllowedPermissionDto { Id = p.Id, Name = p.Name })
                    .ToList()
            });
        }
    }
}
