using Domain.Entities.BaseEntity;
using Domain.Enums;

namespace Domain.Entities
{
    public class DeviceEntity : Entity
    {
        public required string SerialNumber { get; set; }

        public string SecretKey { get; set; } = Guid.NewGuid().ToString("N");

        public DeviceType DeviceType { get; set; }

        public string? Model { get; set; }

        public string? FirmwareVersion { get; set; }

        public long StationId { get; set; }

        public StationEntity? Station { get; set; }

        public bool IsOnline { get; set; } = false;

        public bool IsActive { get; set; } = true;

        /// <summary>Qurilmadan kelgan oxirgi MQTT signal vaqti (heartbeat / telemetry).</summary>
        public DateTime? LastSeenAt { get; set; }

        public ICollection<ProductEntity>? Products { get; set; }

        public ICollection<SessionEntity>? Sessions { get; set; }
    }
}
