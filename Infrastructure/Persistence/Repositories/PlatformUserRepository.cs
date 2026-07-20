using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;
using Persistence.Extensions;

namespace Persistence.Repositories
{
    public class PlatformUserRepository : IPlatformUserRepository
    {
        private readonly AppDbContext _context;

        public PlatformUserRepository(AppDbContext context)
            => _context = context;

        public async Task<PlatformUserEntity?> GetByIdAsync(long userId)
            => await _context.PlatformUsers
                .Include(u => u.Merchant)
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == userId);

        public async Task<PlatformUserEntity?> GetByPhoneNumberAsync(string phoneNumber)
            => await _context.PlatformUsers
                .Include(u => u.Merchant)
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);

        public Task<PagedResult<PlatformUserEntity>> GetAllAsync(PaginationParams param, long? excludeUserId = null)
        {
            var query = _context.PlatformUsers
                .Include(u => u.Role)
                .Include(u => u.Merchant)
                .AsQueryable();

            if (excludeUserId.HasValue)
                query = query.Where(u => u.Id != excludeUserId.Value);

            return query.ApplyListQuery(param).ToPagedResultAsync(param);
        }

        public Task<PagedResult<PlatformUserEntity>> GetByMerchantAsync(long merchantId, PaginationParams param, long? excludeUserId = null)
        {
            var query = _context.PlatformUsers
                .Include(u => u.Role)
                .Include(u => u.Merchant)
                .Where(u => u.MerchantId == merchantId);

            if (excludeUserId.HasValue)
                query = query.Where(u => u.Id != excludeUserId.Value);

            return query.ApplyListQuery(param).ToPagedResultAsync(param);
        }

        public async Task<PlatformUserEntity> CreateAsync(PlatformUserEntity user)
        {
            await _context.PlatformUsers.AddAsync(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<PlatformUserEntity> UpdateAsync(PlatformUserEntity user)
        {
            _context.PlatformUsers.Update(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task DeleteAsync(long userId)
        {
            var user = await _context.PlatformUsers.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null)
                return;

            user.IsDeleted = true;
            await _context.SaveChangesAsync();
        }
    }
}
