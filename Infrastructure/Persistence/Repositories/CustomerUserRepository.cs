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
                .Include(u => u.Role)
                .Include(u => u.Organization)
                .OrderBy(u => u.PhoneNumber)
                .ToPagedResultAsync(param);

        public Task<PagedResult<CustomerUserEntity>> GetByOrganizationAsync(long organizationId, PaginationParams param)
            => _context.CustomerUsers
                .Include(u => u.Role)
                .Include(u => u.Organization)
                .Where(u => u.OrganizationId == organizationId)
                .OrderBy(u => u.PhoneNumber)
                .ToPagedResultAsync(param);

        public async Task<CustomerUserEntity> CreateAsync(CustomerUserEntity user)
        {
            await _context.CustomerUsers.AddAsync(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<CustomerUserEntity> UpdateAsync(CustomerUserEntity user)
        {
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
    }
}
