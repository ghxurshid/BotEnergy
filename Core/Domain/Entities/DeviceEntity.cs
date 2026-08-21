using Domain.Attributes;
using Domain.Entities.BaseEntity;
using Domain.Enums;

namespace Domain.Entities
{
    public class DeviceEntity : Entity
    {
        public required string SerialNumber { get; set; }

        [NotSearchable]
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

        /// <summary>
        /// Qurilma boxidagi naqd pul qoldig'i (UZS). Naqd → karta sessiyasi muvaffaqiyatli
        /// yakunlanganda oshadi, inkassator tasdiqlaganda nolga tushadi.
        /// HECH QACHON entity ustida read-modify-write qilinmaydi — faqat
        /// <c>IDeviceRepository.AddCashAsync</c> / <c>CollectCashAsync</c> orqali (FOR UPDATE lock).
        /// </summary>
        public decimal CashBalance { get; set; }

        /// <summary>Oxirgi marta inkassatsiya qilingan vaqt.</summary>
        public DateTime? CashLastCollectedAt { get; set; }

        public ICollection<ProductEntity>? Products { get; set; }

        public ICollection<SessionEntity>? Sessions { get; set; }
    }
}
