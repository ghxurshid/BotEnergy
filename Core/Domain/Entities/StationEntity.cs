using Domain.Entities.BaseEntity;
using NetTopologySuite.Geometries;

namespace Domain.Entities
{
    public class StationEntity : Entity
    {
        public required string Name { get; set; }

        /// <summary>Matnli manzil (masalan "Toshkent, Yunusobod tumani"). Majburiy.</summary>
        public required string Address { get; set; }

        /// <summary>Geografik koordinata (PostGIS geography Point, SRID 4326). Majburiy.
        /// X = uzunlik (longitude), Y = kenglik (latitude).</summary>
        public required Point Coordinates { get; set; }

        public long MerchantId { get; set; }

        public MerchantEntity? Merchant { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<DeviceEntity>? Devices { get; set; }
    }
}
