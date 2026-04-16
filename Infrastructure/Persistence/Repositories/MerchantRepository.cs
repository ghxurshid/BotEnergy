using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories
{
    public class MerchantRepository : IMerchantRepository
    {
        private readonly AppDbContext _context;

        public MerchantRepository(AppDbContext context)
            => _context = context;

        public async Task<MerchantEntity?> GetByIdAsync(long id)
            => await _context.Merchants.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

        public async Task<List<MerchantEntity>> GetAllAsync()
            => await _context.Merchants.Where(c => !c.IsDeleted).OrderBy(c => c.CompanyName).ToListAsync();

        public async Task<MerchantEntity?> GetByPhoneNumberAsync(string phoneNumber)
            => await _context.Merchants.FirstOrDefaultAsync(c => c.PhoneNumber == phoneNumber && !c.IsDeleted);

        public async Task<MerchantEntity> CreateAsync(MerchantEntity merchant)
        {
            await _context.Merchants.AddAsync(merchant);
            await _context.SaveChangesAsync();
            return merchant;
        }

        public async Task<MerchantEntity> UpdateAsync(MerchantEntity merchant)
        {
            _context.Merchants.Update(merchant);
            await _context.SaveChangesAsync();
            return merchant;
        }

        public async Task DeleteAsync(long id)
        {
            var merchant = await _context.Merchants.FirstOrDefaultAsync(c => c.Id == id);
            if (merchant is null) return;
            merchant.IsDeleted = true;
            merchant.IsActive = false;
            await _context.SaveChangesAsync();
        }
    }
}
