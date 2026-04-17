using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly AppDbContext _context;

        public ProductRepository(AppDbContext context)
            => _context = context;

        public async Task<ProductEntity?> GetByIdAsync(long id)
        {
            return await _context.Products
                .Include(p => p.Device)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        }

        public async Task<ProductEntity?> GetByTypeAsync(ProductType type)
        {
            return await _context.Products
                .Include(p => p.Device)
                .FirstOrDefaultAsync(p => p.Type == type && p.IsActive && !p.IsDeleted);
        }

        public async Task<List<ProductEntity>> GetAllAsync()
            => await _context.Products
                .Include(p => p.Device)
                .Where(p => !p.IsDeleted)
                .OrderBy(p => p.Name)
                .ToListAsync();

        public async Task<List<ProductEntity>> GetByDeviceIdAsync(long deviceId)
            => await _context.Products
                .Include(p => p.Device)
                .Where(p => p.DeviceId == deviceId && !p.IsDeleted)
                .OrderBy(p => p.Name)
                .ToListAsync();

        public async Task<ProductEntity> CreateAsync(ProductEntity product)
        {
            await _context.Products.AddAsync(product);
            await _context.SaveChangesAsync();
            return product;
        }

        public async Task<ProductEntity> UpdateAsync(ProductEntity product)
        {
            _context.Products.Update(product);
            await _context.SaveChangesAsync();
            return product;
        }

        public async Task DeleteAsync(long id)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);
            if (product is null) return;
            product.IsDeleted = true;
            product.IsActive = false;
            await _context.SaveChangesAsync();
        }
    }
}
