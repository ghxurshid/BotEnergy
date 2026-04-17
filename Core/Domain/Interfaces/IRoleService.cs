using Domain.Dtos;
using Domain.Dtos.Base;

namespace Domain.Interfaces
{
    public interface IRoleService
    {
        Task<GenericDto<CreateRoleResultDto>> CreateRoleAsync(CreateRoleDto dto);
        Task<GenericDto<GetRolesResultDto>> GetRolesAsync();
        Task<GenericDto<RoleItemDto>> GetRoleByIdAsync(long id);
        Task<GenericDto<RoleResultDto>> UpdateRoleAsync(long id, UpdateRoleDto dto);
        Task<GenericDto<RoleResultDto>> DeleteRoleAsync(long id);
        Task<GenericDto<AddPermissionResultDto>> AddPermissionToRoleAsync(AddPermissionDto dto);
        Task<GenericDto<RemovePermissionResultDto>> RemovePermissionFromRoleAsync(RemovePermissionDto dto);
        Task<GenericDto<AssignRoleResultDto>> AssignRoleToUserAsync(AssignRoleDto dto);
        Task<GenericDto<GetRolePermissionsResultDto>> GetRolePermissionsAsync(long roleId);
    }
}
