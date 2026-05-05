using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Enums;

namespace Domain.Interfaces
{
    public interface IRoleService
    {
        Task<GenericDto<CreateRoleResultDto>> CreateRoleAsync(CreateRoleDto dto, long callerId, HashSet<string> callerPermissions);
        Task<GenericDto<GetRolesResultDto>> GetRolesAsync(long callerId, HashSet<string> callerPermissions);
        Task<GenericDto<RoleItemDto>> GetRoleByIdAsync(long id, long callerId, HashSet<string> callerPermissions);
        Task<GenericDto<RoleResultDto>> UpdateRoleAsync(long id, UpdateRoleDto dto, long callerId, HashSet<string> callerPermissions);
        Task<GenericDto<RoleResultDto>> DeleteRoleAsync(long id, long callerId, HashSet<string> callerPermissions);
        Task<GenericDto<GetRolePermissionsResultDto>> GetRolePermissionsAsync(long roleId, long callerId, HashSet<string> callerPermissions);
        Task<GenericDto<GetAllowedPermissionsResultDto>> GetAllowedPermissionsAsync(RoleType roleType, long callerId, HashSet<string> callerPermissions);
    }
}
