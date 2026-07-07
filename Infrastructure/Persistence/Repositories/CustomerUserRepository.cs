using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;
using Persistence.Extensions;

namespace Persistence.Repositories
{
    public class CustomerUserRepository : ICustomerUserRepository
    {
        private readonly AppDbContext _context;

        public CustomerUserRepository(AppDbContext context)
            => _context = context;

        public async Task<CustomerUserEntity?> GetByIdAsync(long userId)
            => await _context.CustomerUsers
                .Include(u => u.Organization)
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == userId);

        public async Task<CustomerUserEntity?> GetByPhoneNumberAsync(string phoneNumber)
            => await _context.CustomerUsers
                .Include(u => u.Organization)
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);

        public Task<PagedResult<CustomerUserEntity>> GetAllAsync(PaginationParams param)
            => _context.CustomerUsers
                .AsNoTracking()
                .Include(u => u.Role)
                .Include(u => u.Organization)
                .ApplyListQuery(param)
                .ToPagedResultAsync(param);

        public Task<PagedResult<CustomerUserEntity>> GetByOrganizationAsync(long organizationId, PaginationParams param)
            => _context.CustomerUsers
                .AsNoTracking()
                .Include(u => u.Role)
                .Include(u => u.Organization)
                .Where(u => u.OrganizationId == organizationId)
                .ApplyListQuery(param)
                .ToPagedResultAsync(param);

        public async Task<CustomerUserEntity> CreateAsync(CustomerUserEntity user)
        {
            await _context.CustomerUsers.AddAsync(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<CustomerUserEntity> UpdateAsync(CustomerUserEntity user)
        {
            if (_context.Entry(user).State == EntityState.Detached)
                _context.CustomerUsers.Update(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task DeleteAsync(long userId)
        {
            var user = await _context.CustomerUsers.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null)
                return;

            user.IsDeleted = true;
            await _context.SaveChangesAsync();
        }

        public async Task<decimal> DeductBalanceAsync(long userId, decimal maxAmount)
        {
            // FOR UPDATE — satrni lock qiladi: shu lock ostidagi relative decrement race-safe.
            // Statement o'zi tranzaksiya bo'lmasa ham, tashqi ITransactionRunner tranzaksiyasida
            // chaqirilganda lock commit'gacha ushlab turiladi.
            var balances = await _context.Database
                .SqlQuery<decimal>($@"SELECT balance AS ""Value"" FROM auth.customer_users WHERE id = {userId} AND is_deleted = false FOR UPDATE")
                .ToListAsync();

            if (balances.Count == 0)
                return 0m;

            var deducted = Math.Min(balances[0], maxAmount);
            if (deducted <= 0)
                return 0m;

            // Balance >= deducted sharti — tranzaksiyasiz chaqirilganda ham manfiy balansdan himoya.
            var now = DateTime.Now;
            var affected = await _context.CustomerUsers
                .Where(u => u.Id == userId && u.Balance >= deducted)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.Balance, u => u.Balance - deducted)
                    .SetProperty(u => u.UpdatedDate, now));

            return affected > 0 ? deducted : 0m;
        }

        public async Task<decimal?> TopUpBalanceAsync(long userId, decimal amount)
        {
            var balances = await _context.Database
                .SqlQuery<decimal>($@"SELECT balance AS ""Value"" FROM auth.customer_users WHERE id = {userId} AND is_deleted = false FOR UPDATE")
                .ToListAsync();

            if (balances.Count == 0)
                return null;

            var now = DateTime.Now;
            await _context.CustomerUsers
                .Where(u => u.Id == userId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.Balance, u => u.Balance + amount)
                    .SetProperty(u => u.UpdatedDate, now));

            return balances[0] + amount;
        }
    }
}
