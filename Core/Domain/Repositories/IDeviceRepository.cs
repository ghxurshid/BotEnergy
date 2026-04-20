using Domain.Dtos.Base;
using Domain.Entities;

namespace Domain.Repositories
{
    public interface IDeviceRepository
    {
        Task<DeviceEntity?> GetByIdAsync(long id);
        Task<DeviceEntity?> GetBySerialNumberAsync(string serialNumber);
        Task<bool> ValidateDeviceAsync(string serialNumber, string secretKey);
        Task<PagedResult<DeviceEntity>> GetAllAsync(PaginationParams param);
        Task<List<DeviceEntity>> GetByStationIdAsync(long stationId);
        Task<DeviceEntity> CreateAsync(DeviceEntity device);
        Task<DeviceEntity> UpdateAsync(DeviceEntity device);
        Task DeleteAsync(long id);
    }
}
