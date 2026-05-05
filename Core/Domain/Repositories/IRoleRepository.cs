using Domain.Entities;
using Domain.Enums;

namespace Domain.Repositories
{
    public interface IRoleRepository
    {
        Task<RoleEntity?> GetByIdAsync(long id);
        Task<RoleEntity?> GetByIdWithPermissionsAsync(long id);
        Task<List<RoleEntity>> GetAllAsync();
        Task<List<RoleEntity>> GetByScopeAsync(IEnumerable<RoleType> roleTypes, long? organizationId, long? merchantId);
        Task<RoleEntity> CreateAsync(RoleEntity role);
        Task<RoleEntity> UpdateAsync(RoleEntity role);
        Task DeleteAsync(long id);
        Task<PermissionEntity?> GetPermissionByNameAsync(string name);
        Task<PermissionEntity?> GetPermissionByIdAsync(long id);
        Task<List<PermissionEntity>> GetAllPermissionsAsync();
        Task<List<long>> FilterExistingPermissionIdsAsync(IEnumerable<long> ids);
        Task<List<string>> GetPermissionsByRoleIdAsync(long roleId);
        Task AddPermissionAsync(RolePermissionEntity permission);
        Task RemovePermissionAsync(long roleId, long permissionId);
        Task<List<string>> GetUserPermissionsAsync(long? roleId);
    }
}
