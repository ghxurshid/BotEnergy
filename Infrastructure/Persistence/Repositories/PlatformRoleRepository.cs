using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories
{
    public class PlatformRoleRepository : IPlatformRoleRepository
    {
        private readonly AppDbContext _context;

        public PlatformRoleRepository(AppDbContext context)
            => _context = context;

        public async Task<PlatformRoleEntity?> GetByIdAsync(long id)
            => await _context.PlatformRoles.FirstOrDefaultAsync(r => r.Id == id);

        public async Task<PlatformRoleEntity?> GetByIdWithPermissionsAsync(long id)
            => await _context.PlatformRoles
                .Include(r => r.RolePermissions)
                .FirstOrDefaultAsync(r => r.Id == id);

        public async Task<List<PlatformRoleEntity>> GetByScopeAsync(bool includeManage, long? merchantId)
            => await _context.PlatformRoles
                .Where(r =>
                    (includeManage && r.MerchantId == null) ||
                    (r.MerchantId != null && (merchantId == null || r.MerchantId == merchantId)))
                .OrderBy(r => r.Name)
                .ToListAsync();

        public async Task<PlatformRoleEntity> CreateAsync(PlatformRoleEntity role)
        {
            await _context.PlatformRoles.AddAsync(role);
            await _context.SaveChangesAsync();
            return role;
        }

        public async Task<PlatformRoleEntity> UpdateAsync(PlatformRoleEntity role)
        {
            _context.PlatformRoles.Update(role);
            await _context.SaveChangesAsync();
            return role;
        }

        public async Task DeleteAsync(long id)
        {
            var role = await _context.PlatformRoles.FirstOrDefaultAsync(r => r.Id == id);
            if (role is null) return;
            role.IsDeleted = true;
            role.IsActive = false;
            await _context.SaveChangesAsync();
        }

        public async Task<List<string>> GetPermissionsByRoleIdAsync(long roleId)
            => await _context.PlatformRolePermissions
                .Where(rp => rp.RoleId == roleId)
                .Include(rp => rp.Permission)
                .Select(rp => rp.Permission!.Name)
                .ToListAsync();

        public async Task<List<string>> GetUserPermissionsAsync(long? roleId)
            => roleId is null
                ? new List<string>()
                : await GetPermissionsByRoleIdAsync(roleId.Value);
    }
}
