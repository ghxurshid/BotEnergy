using Domain.Entities;

namespace Domain.Repositories
{
    public interface IRoleRepository
    {
        Task<RoleEntity?> GetByIdAsync(long id);
        Task<List<RoleEntity>> GetAllAsync();
        Task<RoleEntity> CreateAsync(RoleEntity role);
        Task<List<string>> GetPermissionsByRoleIdAsync(long roleId);
        Task AddPermissionAsync(RolePermissionEntity permission);
        Task RemovePermissionAsync(long roleId, string permission);
        Task<List<string>> GetUserPermissionsAsync(long? roleId);
    }
}
