using Domain.Entities.BaseEntity;

namespace Domain.Entities
{
    public class DeviceEntity : Entity
    {
        public required string SerialNumber { get; set; }

        public string? Model { get; set; }

        public string? FirmwareVersion { get; set; }

        public long StationId { get; set; }

        public StationEntity? Station { get; set; }

        public bool IsOnline { get; set; } = false;

        public bool IsActive { get; set; } = true;
    }
}
