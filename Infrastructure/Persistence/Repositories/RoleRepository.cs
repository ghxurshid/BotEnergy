using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories
{
    public class RoleRepository : IRoleRepository
    {
        private readonly AppDbContext _context;

        public RoleRepository(AppDbContext context)
            => _context = context;

        public async Task<RoleEntity?> GetByIdAsync(long id)
            => await _context.Roles
                .FirstOrDefaultAsync(r => r.Id == id);

        public async Task<List<RoleEntity>> GetAllAsync()
            => await _context.Roles
                .OrderBy(r => r.Name)
                .ToListAsync();

        public async Task<RoleEntity> CreateAsync(RoleEntity role)
        {
            await _context.Roles.AddAsync(role);
            await _context.SaveChangesAsync();
            return role;
        }

        public async Task<RoleEntity> UpdateAsync(RoleEntity role)
        {
            _context.Roles.Update(role);
            await _context.SaveChangesAsync();
            return role;
        }

        public async Task DeleteAsync(long id)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == id);
            if (role is null) return;
            role.IsDeleted = true;
            role.IsActive = false;
            await _context.SaveChangesAsync();
        }

        public async Task<List<string>> GetPermissionsByRoleIdAsync(long roleId)
            => await _context.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .Include(rp => rp.Permission)
                .Select(rp => rp.Permission!.Name)
                .ToListAsync();

        public async Task AddPermissionAsync(RolePermissionEntity permission)
        {
            await _context.RolePermissions.AddAsync(permission);
            await _context.SaveChangesAsync();
        }

        public async Task<PermissionEntity?> GetPermissionByNameAsync(string name)
            => await _context.Permissions
                .FirstOrDefaultAsync(p => p.Name == name);

        public async Task<PermissionEntity?> GetPermissionByIdAsync(long id)
            => await _context.Permissions
                .FirstOrDefaultAsync(p => p.Id == id);

        public async Task<List<PermissionEntity>> GetAllPermissionsAsync()
            => await _context.Permissions
                .OrderBy(p => p.Name)
                .ToListAsync();

        public async Task RemovePermissionAsync(long roleId, long permissionId)
        {
            var entity = await _context.RolePermissions
                .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);

            if (entity is null)
                return;

            entity.IsDeleted = true;
            await _context.SaveChangesAsync();
        }

        public async Task<List<string>> GetUserPermissionsAsync(long? roleId)
        {
            if (roleId is null)
                return new List<string>();

            return await GetPermissionsByRoleIdAsync(roleId.Value);
        }
    }
}
