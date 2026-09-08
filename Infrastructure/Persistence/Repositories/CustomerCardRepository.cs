using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories
{
    public class CustomerCardRepository : ICustomerCardRepository
    {
        private readonly AppDbContext _context;

        public CustomerCardRepository(AppDbContext context)
            => _context = context;

        public async Task<CustomerCardEntity> CreateAsync(CustomerCardEntity card)
        {
            await _context.CustomerCards.AddAsync(card);
            await _context.SaveChangesAsync();
            return card;
        }

        public Task<CustomerCardEntity?> GetByIdAsync(long id)
            => _context.CustomerCards.FirstOrDefaultAsync(c => c.Id == id);

        public Task<List<CustomerCardEntity>> GetForUserAsync(long userId, long? merchantId = null)
        {
            var query = _context.CustomerCards.Where(c => c.UserId == userId);
            if (merchantId.HasValue)
                query = query.Where(c => c.MerchantId == merchantId.Value);

            return query
                .OrderByDescending(c => c.IsDefault)
                .ThenByDescending(c => c.CreatedDate)
                .ToListAsync();
        }

        public Task<CustomerCardEntity?> GetUsableAsync(long userId, long merchantId)
            => _context.CustomerCards
                .Where(c => c.UserId == userId && c.MerchantId == merchantId && c.IsVerified)
                .OrderByDescending(c => c.IsDefault)
                .ThenByDescending(c => c.CreatedDate)
                .FirstOrDefaultAsync();

        public Task<CustomerCardEntity?> GetByTokenAsync(long merchantId, string token)
            => _context.CustomerCards
                .FirstOrDefaultAsync(c => c.MerchantId == merchantId && c.Token == token);

        public async Task<bool> SetDefaultAsync(long userId, long merchantId, long cardId)
        {
            var now = DateTime.Now;

            // Avval hammasini bekor qilamiz, keyin bittasini yoqamiz — ikki ExecuteUpdate,
            // shuning uchun "bir vaqtda ikkita default" oralig'i bo'lmasligi uchun tartib muhim.
            await _context.CustomerCards
                .Where(c => c.UserId == userId && c.MerchantId == merchantId && c.IsDefault)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.IsDefault, false)
                    .SetProperty(c => c.UpdatedDate, now));

            var affected = await _context.CustomerCards
                .Where(c => c.Id == cardId && c.UserId == userId && c.MerchantId == merchantId && c.IsVerified)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.IsDefault, true)
                    .SetProperty(c => c.UpdatedDate, now));

            return affected > 0;
        }

        public async Task UpdateAsync(CustomerCardEntity card)
        {
            if (_context.Entry(card).State == EntityState.Detached)
                _context.CustomerCards.Update(card);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(long id)
        {
            var card = await _context.CustomerCards.FirstOrDefaultAsync(c => c.Id == id);
            if (card is null) return false;

            card.IsDeleted = true;
            card.IsDefault = false;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
