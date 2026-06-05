using Domain.Auth;
using Domain.Dtos;
using Domain.Dtos.Base;

namespace Domain.Interfaces
{
    /// <summary>Customer (Corporate) rollarini boshqarish — tashkilot scope ichida.</summary>
    public interface ICustomerRoleService
    {
        Task<GenericDto<CreateRoleResultDto>> CreateRoleAsync(CreateRoleDto dto, AccessScope scope);
        Task<GenericDto<GetRolesResultDto>> GetRolesAsync(AccessScope scope);
        Task<GenericDto<RoleItemDto>> GetRoleByIdAsync(long id, AccessScope scope);
        Task<GenericDto<RoleResultDto>> UpdateRoleAsync(long id, UpdateRoleDto dto, AccessScope scope);
        Task<GenericDto<RoleResultDto>> DeleteRoleAsync(long id, AccessScope scope);
        Task<GenericDto<GetRolePermissionsResultDto>> GetRolePermissionsAsync(long roleId, AccessScope scope);
        Task<GenericDto<GetAllowedPermissionsResultDto>> GetAllowedPermissionsAsync();
    }
}
