using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;
using Persistence.Extensions;

namespace Persistence.Repositories
{
    public class OrganizationRepository : IOrganizationRepository
    {
        private readonly AppDbContext _context;

        public OrganizationRepository(AppDbContext context)
            => _context = context;

        public async Task<OrganizationEntity?> GetByIdAsync(long id)
            => await _context.Organizations.FirstOrDefaultAsync(o => o.Id == id);

        public Task<PagedResult<OrganizationEntity>> GetAllAsync(PaginationParams param)
            => _context.Organizations
                .OrderBy(o => o.Name)
                .ToPagedResultAsync(param);

        public async Task<OrganizationEntity> CreateAsync(OrganizationEntity organization)
        {
            await _context.Organizations.AddAsync(organization);
            await _context.SaveChangesAsync();
            return organization;
        }

        public async Task<OrganizationEntity> UpdateAsync(OrganizationEntity organization)
        {
            _context.Organizations.Update(organization);
            await _context.SaveChangesAsync();
            return organization;
        }

        public async Task DeleteAsync(long id)
        {
            var org = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == id);
            if (org is null) return;
            org.IsDeleted = true;
            org.IsActive = false;
            await _context.SaveChangesAsync();
        }
    }
}
