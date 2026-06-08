using Domain.Dtos.Base;
using Domain.Dtos.Device;
using Domain.Entities;

namespace Domain.Repositories
{
    public interface IDeviceRepository
    {
        Task<DeviceEntity?> GetByIdAsync(long id);
        Task<DeviceEntity?> GetBySerialNumberAsync(string serialNumber);
        Task<bool> ValidateDeviceAsync(string serialNumber, string secretKey);
        Task<PagedResult<DeviceEntity>> GetAllAsync(PaginationParams param, long? merchantId = null);
        Task<List<DeviceEntity>> GetByStationIdAsync(long stationId);
        Task<DeviceEntity> CreateAsync(DeviceEntity device);
        Task<DeviceEntity> UpdateAsync(DeviceEntity device);
        Task DeleteAsync(long id);

        /// <summary>
        /// Inbound kelganda chaqiriladi — LastSeenAt ni yangilaydi va, agar qurilma oldin
        /// offline bo'lgan bo'lsa, IsOnline=true ga o'tkazadi. Atomik SQL (entity yuklamaydi).
        /// </summary>
        /// <returns><c>true</c> — offline→online edge yuz berdi (reconnect); <c>false</c> — allaqachon online edi.</returns>
        Task<bool> MarkSeenAsync(string serialNumber);

        /// <summary>
        /// LastSeenAt belgilangan vaqtdan eskirib qolgan, lekin hali IsOnline=true bo'lgan qurilmalarni topadi
        /// (Station bilan — MerchantId uchun).
        /// </summary>
        Task<List<DeviceEntity>> GetStaleOnlineDevicesAsync(DateTime threshold);

        /// <summary>Bitta qurilma holati (scope/event uchun yengil proyeksiya).</summary>
        Task<DeviceStatusInfo?> GetStatusInfoBySerialAsync(string serialNumber);
        Task<DeviceStatusInfo?> GetStatusInfoByIdAsync(long deviceId);

        /// <summary>Merchantning barcha aktiv qurilmalari holati (admin snapshot uchun).</summary>
        Task<List<DeviceStatusInfo>> GetStatusInfoByMerchantAsync(long merchantId);
    }
}
