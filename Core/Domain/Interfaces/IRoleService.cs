using Domain.Auth;
using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Enums;

namespace Domain.Interfaces
{
    /// <summary>Platform (Manage/Merchant) rollarini boshqarish.</summary>
    public interface IRoleService
    {
        Task<GenericDto<CreateRoleResultDto>> CreateRoleAsync(CreateRoleDto dto, AccessScope scope);
        Task<GenericDto<GetRolesResultDto>> GetRolesAsync(AccessScope scope);
        Task<GenericDto<RoleItemDto>> GetRoleByIdAsync(long id, AccessScope scope);
        Task<GenericDto<RoleResultDto>> UpdateRoleAsync(long id, UpdateRoleDto dto, AccessScope scope);
        Task<GenericDto<RoleResultDto>> DeleteRoleAsync(long id, AccessScope scope);
        Task<GenericDto<GetRolePermissionsResultDto>> GetRolePermissionsAsync(long roleId, AccessScope scope);
        Task<GenericDto<GetAllowedPermissionsResultDto>> GetAllowedPermissionsAsync(RoleKind kind, AccessScope scope);
    }
}
