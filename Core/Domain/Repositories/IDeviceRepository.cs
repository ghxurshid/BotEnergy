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

        /// <summary>
        /// Heartbeat / telemetry kelganda chaqiriladi — LastSeenAt va IsOnline ni yangilaydi.
        /// Atomic SQL update — entity yuklab kelmasdan ishlaydi.
        /// </summary>
        Task<int> TouchLastSeenAsync(string serialNumber);

        /// <summary>
        /// LastSeenAt belgilangan vaqtdan eskirib qolgan, lekin hali IsOnline=true bo'lgan qurilmalarni topadi.
        /// </summary>
        Task<List<DeviceEntity>> GetStaleOnlineDevicesAsync(DateTime threshold);
    }
}
