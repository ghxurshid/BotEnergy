using Domain.Entities;
using Domain.Enums;

namespace Domain.Repositories
{
    public interface IProductRepository
    {
        Task<ProductEntity?> GetByIdAsync(long id);
        Task<ProductEntity?> GetByTypeAsync(ProductType type);
        Task CreateAsync(ProductEntity product);
    }
}
