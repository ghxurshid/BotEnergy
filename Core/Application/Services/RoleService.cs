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
        private readonly IUserRepository _userRepository;

        public RoleService(IRoleRepository roleRepository, IUserRepository userRepository)
        {
            _roleRepository = roleRepository;
            _userRepository = userRepository;
        }

        public async Task<GenericDto<CreateRoleResultDto>> CreateRoleAsync(CreateRoleDto dto)
        {
            var role = new RoleEntity
            {
                Name = dto.Name,
                Description = dto.Description,
                IsActive = true
            };

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

        public async Task<GenericDto<AddPermissionResultDto>> AddPermissionToRoleAsync(AddPermissionDto dto)
        {
            var role = await _roleRepository.GetByIdAsync(dto.RoleId);
            if (role is null)
                return GenericDto<AddPermissionResultDto>.Error(404, "Rol topilmadi.");

            var existing = await _roleRepository.GetPermissionsByRoleIdAsync(dto.RoleId);
            if (existing.Contains(dto.Permission))
                return GenericDto<AddPermissionResultDto>.Error(400, "Bu permission allaqachon berilgan.");

            var permission = await _roleRepository.GetPermissionByNameAsync(dto.Permission);
            if (permission is null)
                return GenericDto<AddPermissionResultDto>.Error(404, "Permission topilmadi.");

            await _roleRepository.AddPermissionAsync(new RolePermissionEntity
            {
                RoleId = dto.RoleId,
                PermissionId = permission.Id
            });

            return GenericDto<AddPermissionResultDto>.Success(new AddPermissionResultDto
            {
                ResultMessage = $"'{dto.Permission}' permissioni muvaffaqiyatli qo'shildi."
            });
        }

        public async Task<GenericDto<RemovePermissionResultDto>> RemovePermissionFromRoleAsync(RemovePermissionDto dto)
        {
            var role = await _roleRepository.GetByIdAsync(dto.RoleId);
            if (role is null)
                return GenericDto<RemovePermissionResultDto>.Error(404, "Rol topilmadi.");

            var permission = await _roleRepository.GetPermissionByNameAsync(dto.Permission);
            if (permission is null)
                return GenericDto<RemovePermissionResultDto>.Error(404, "Permission topilmadi.");

            await _roleRepository.RemovePermissionAsync(dto.RoleId, permission.Id);

            return GenericDto<RemovePermissionResultDto>.Success(new RemovePermissionResultDto
            {
                ResultMessage = $"'{dto.Permission}' permissioni muvaffaqiyatli o'chirildi."
            });
        }

        public async Task<GenericDto<AssignRoleResultDto>> AssignRoleToUserAsync(AssignRoleDto dto)
        {
            var user = await _userRepository.GetByPhoneNumberAsync(dto.PhoneNumber);
            if (user is null)
                return GenericDto<AssignRoleResultDto>.Error(404, "Foydalanuvchi topilmadi.");

            var role = await _roleRepository.GetByIdAsync(dto.RoleId);
            if (role is null)
                return GenericDto<AssignRoleResultDto>.Error(404, "Rol topilmadi.");

            user.RoleId = dto.RoleId;
            await _userRepository.UpdateUserAsync(user);

            return GenericDto<AssignRoleResultDto>.Success(new AssignRoleResultDto
            {
                ResultMessage = $"Foydalanuvchiga '{role.Name}' roli muvaffaqiyatli berildi."
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
    }
}
