using Domain.Entities.BaseEntity;
using Domain.Enums;

namespace Domain.Entities
{
    public class DeviceEntity : Entity
    {
        public required string SerialNumber { get; set; }

        public string SecretKey { get; set; } = Guid.NewGuid().ToString("N");

        public DeviceType DeviceType { get; set; }

        public int FunctionCount { get; set; } = 1;

        public string? Model { get; set; }

        public string? FirmwareVersion { get; set; }

        public long StationId { get; set; }

        public StationEntity? Station { get; set; }

        public bool IsOnline { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public ICollection<ProductEntity>? Products { get; set; }

        public ICollection<UsageSessionEntity>? UsageSessions { get; set; }
    }
}
