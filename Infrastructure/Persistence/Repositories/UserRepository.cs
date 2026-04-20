using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;
using Persistence.Extensions;

namespace Persistence.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;

        public UserRepository(AppDbContext context)
            => _context = context;

        public async Task<UserEntity?> GetByIdAsync(long userId)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is LegalUserEntity legalUser)
                await _context.Entry(legalUser).Reference(l => l.Organization).LoadAsync();

            return user;
        }

        public async Task<UserEntity?> GetByPhoneNumberAsync(string phoneNumber)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
        }

        public Task<PagedResult<UserEntity>> GetAllAsync(PaginationParams param)
            => _context.Users
                .Include(u => u.Role)
                .OrderBy(u => u.PhoneNumber)
                .ToPagedResultAsync(param);

        public async Task<UserEntity> CreateUserAsync(UserEntity user)
        {
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<UserEntity> UpdateUserAsync(UserEntity user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task DeleteUserAsync(long userId)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null)
                return;

            user.IsDeleted = true;
            await _context.SaveChangesAsync();
        }
    }
}
