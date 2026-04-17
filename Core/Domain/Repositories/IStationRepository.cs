using Domain.Entities;

namespace Domain.Repositories
{
    public interface IStationRepository
    {
        Task<StationEntity?> GetByIdAsync(long id);
        Task<List<StationEntity>> GetAllAsync();
        Task<List<StationEntity>> GetByMerchantIdAsync(long merchantId);
        Task<StationEntity> CreateAsync(StationEntity station);
        Task<StationEntity> UpdateAsync(StationEntity station);
        Task DeleteAsync(long id);
    }
}
