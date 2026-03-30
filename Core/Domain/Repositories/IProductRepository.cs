using Domain.Entities;
using Domain.Enums;

namespace Domain.Repositories
{
    public interface IProductRepository
    {
        Task<ProductEntity?> GetByTypeAsync(ProductType type);
    }
}
