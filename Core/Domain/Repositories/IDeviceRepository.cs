using Domain.Entities;

namespace Domain.Repositories
{
    public interface IDeviceRepository
    {
        Task<DeviceEntity?> GetBySerialNumberAsync(string serialNumber);
    }
}
