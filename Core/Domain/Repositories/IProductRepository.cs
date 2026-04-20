using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Enums;

namespace Domain.Repositories
{
    public interface IProductRepository
    {
        Task<ProductEntity?> GetByIdAsync(long id);
        Task<ProductEntity?> GetByTypeAsync(ProductType type);
        Task<PagedResult<ProductEntity>> GetAllAsync(PaginationParams param);
        Task<List<ProductEntity>> GetByDeviceIdAsync(long deviceId);
        Task<ProductEntity> CreateAsync(ProductEntity product);
        Task<ProductEntity> UpdateAsync(ProductEntity product);
        Task DeleteAsync(long id);
    }
}
