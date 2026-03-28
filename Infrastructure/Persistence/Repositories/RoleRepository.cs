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
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

        public async Task<List<RoleEntity>> GetAllAsync()
            => await _context.Roles
                .Where(r => !r.IsDeleted && r.IsActive)
                .ToListAsync();

        public async Task<RoleEntity> CreateAsync(RoleEntity role)
        {
            await _context.Roles.AddAsync(role);
            await _context.SaveChangesAsync();
            return role;
        }

        public async Task<List<string>> GetPermissionsByRoleIdAsync(long roleId)
            => await _context.RolePermissions
                .Where(rp => rp.RoleId == roleId && !rp.IsDeleted)
                .Select(rp => rp.Permission)
                .ToListAsync();

        public async Task AddPermissionAsync(RolePermissionEntity permission)
        {
            await _context.RolePermissions.AddAsync(permission);
            await _context.SaveChangesAsync();
        }

        public async Task RemovePermissionAsync(long roleId, string permission)
        {
            var entity = await _context.RolePermissions
                .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.Permission == permission && !rp.IsDeleted);

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
