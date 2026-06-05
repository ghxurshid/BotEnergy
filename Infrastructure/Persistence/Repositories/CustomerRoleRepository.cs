using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories
{
    public class CustomerRoleRepository : ICustomerRoleRepository
    {
        private readonly AppDbContext _context;

        public CustomerRoleRepository(AppDbContext context)
            => _context = context;

        public async Task<CustomerRoleEntity?> GetByIdAsync(long id)
            => await _context.CustomerRoles.FirstOrDefaultAsync(r => r.Id == id);

        public async Task<CustomerRoleEntity?> GetByIdWithPermissionsAsync(long id)
            => await _context.CustomerRoles
                .Include(r => r.RolePermissions)
                .FirstOrDefaultAsync(r => r.Id == id);

        public async Task<List<CustomerRoleEntity>> GetByScopeAsync(bool includeNatural, long? organizationId)
            => await _context.CustomerRoles
                .Where(r =>
                    (includeNatural && r.OrganizationId == null) ||
                    (r.OrganizationId != null && (organizationId == null || r.OrganizationId == organizationId)))
                .OrderBy(r => r.Name)
                .ToListAsync();

        public async Task<CustomerRoleEntity?> GetDefaultNaturalRoleAsync()
            => await _context.CustomerRoles
                .Where(r => r.OrganizationId == null && r.IsActive)
                .OrderBy(r => r.Id)
                .FirstOrDefaultAsync();

        public async Task<CustomerRoleEntity> CreateAsync(CustomerRoleEntity role)
        {
            await _context.CustomerRoles.AddAsync(role);
            await _context.SaveChangesAsync();
            return role;
        }

        public async Task<CustomerRoleEntity> UpdateAsync(CustomerRoleEntity role)
        {
            _context.CustomerRoles.Update(role);
            await _context.SaveChangesAsync();
            return role;
        }

        public async Task DeleteAsync(long id)
        {
            var role = await _context.CustomerRoles.FirstOrDefaultAsync(r => r.Id == id);
            if (role is null) return;
            role.IsDeleted = true;
            role.IsActive = false;
            await _context.SaveChangesAsync();
        }

        public async Task<List<string>> GetPermissionsByRoleIdAsync(long roleId)
            => await _context.CustomerRolePermissions
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
