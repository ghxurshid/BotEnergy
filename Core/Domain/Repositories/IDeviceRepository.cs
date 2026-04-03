using Domain.Entities;

namespace Domain.Repositories
{
    public interface IDeviceRepository
    {
        Task<DeviceEntity?> GetByIdAsync(long id);
        Task<DeviceEntity?> GetBySerialNumberAsync(string serialNumber);
        Task<List<DeviceEntity>> GetAllAsync();
        Task<List<DeviceEntity>> GetByStationIdAsync(long stationId);
        Task<DeviceEntity> CreateAsync(DeviceEntity device);
        Task<DeviceEntity> UpdateAsync(DeviceEntity device);
        Task DeleteAsync(long id);
    }
}
