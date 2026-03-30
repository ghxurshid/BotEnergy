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

        public async Task<ProductEntity?> GetByTypeAsync(ProductType type)
        {
            return await _context.Products
                .FirstOrDefaultAsync(p => p.Type == type && p.IsActive && !p.IsDeleted);
        }
    }
}
