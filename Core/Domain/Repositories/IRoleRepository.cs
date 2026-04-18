using Domain.Entities;

namespace Domain.Repositories
{
    public interface IRoleRepository
    {
        Task<RoleEntity?> GetByIdAsync(long id);
        Task<List<RoleEntity>> GetAllAsync();
        Task<RoleEntity> CreateAsync(RoleEntity role);
        Task<RoleEntity> UpdateAsync(RoleEntity role);
        Task DeleteAsync(long id);
        Task<PermissionEntity?> GetPermissionByNameAsync(string name);
        Task<PermissionEntity?> GetPermissionByIdAsync(long id);
        Task<List<PermissionEntity>> GetAllPermissionsAsync();
        Task<List<string>> GetPermissionsByRoleIdAsync(long roleId);
        Task AddPermissionAsync(RolePermissionEntity permission);
        Task RemovePermissionAsync(long roleId, long permissionId);
        Task<List<string>> GetUserPermissionsAsync(long? roleId);
    }
}
