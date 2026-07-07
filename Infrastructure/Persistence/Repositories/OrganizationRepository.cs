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

        public Task<PagedResult<OrganizationEntity>> GetAllAsync(PaginationParams param, long? organizationId = null)
            => _context.Organizations
                .Where(o => organizationId == null || o.Id == organizationId)
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
            if (_context.Entry(organization).State == EntityState.Detached)
                _context.Organizations.Update(organization);
            await _context.SaveChangesAsync();
            return organization;
        }

        public async Task<decimal> DeductBalanceAsync(long organizationId, decimal maxAmount)
        {
            var balances = await _context.Database
                .SqlQuery<decimal>($@"SELECT balance AS ""Value"" FROM auth.organizations WHERE id = {organizationId} AND is_deleted = false FOR UPDATE")
                .ToListAsync();

            if (balances.Count == 0)
                return 0m;

            var deducted = Math.Min(balances[0], maxAmount);
            if (deducted <= 0)
                return 0m;

            var now = DateTime.Now;
            var affected = await _context.Organizations
                .Where(o => o.Id == organizationId && o.Balance >= deducted)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Balance, o => o.Balance - deducted)
                    .SetProperty(o => o.UpdatedDate, now));

            return affected > 0 ? deducted : 0m;
        }

        public async Task<decimal?> TopUpBalanceAsync(long organizationId, decimal amount)
        {
            var balances = await _context.Database
                .SqlQuery<decimal>($@"SELECT balance AS ""Value"" FROM auth.organizations WHERE id = {organizationId} AND is_deleted = false FOR UPDATE")
                .ToListAsync();

            if (balances.Count == 0)
                return null;

            var now = DateTime.Now;
            await _context.Organizations
                .Where(o => o.Id == organizationId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Balance, o => o.Balance + amount)
                    .SetProperty(o => o.UpdatedDate, now));

            return balances[0] + amount;
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
