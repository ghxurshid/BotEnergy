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
        Task<GenericDto<GetRolePermissionsResultDto>> GetRolePermissionsAsync(long roleId);
        Task<GenericDto<GetAllowedPermissionsResultDto>> GetAllowedPermissionsAsync();
    }
}
