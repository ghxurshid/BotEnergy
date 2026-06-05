using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories
{
    public class PermissionRepository : IPermissionRepository
    {
        private readonly AppDbContext _context;

        public PermissionRepository(AppDbContext context)
            => _context = context;

        public async Task<PermissionEntity?> GetByNameAsync(string name)
            => await _context.Permissions.FirstOrDefaultAsync(p => p.Name == name);

        public async Task<PermissionEntity?> GetByIdAsync(long id)
            => await _context.Permissions.FirstOrDefaultAsync(p => p.Id == id);

        public async Task<List<PermissionEntity>> GetAllAsync()
            => await _context.Permissions.OrderBy(p => p.Name).ToListAsync();

        public async Task<List<long>> FilterExistingIdsAsync(IEnumerable<long> ids)
            => await _context.Permissions
                .Where(p => ids.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync();
    }
}
