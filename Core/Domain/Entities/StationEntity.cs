using Domain.Entities.BaseEntity;

namespace Domain.Entities
{
    public class StationEntity : Entity
    {
        public required string Name { get; set; }

        public string? Location { get; set; }

        public long MerchantId { get; set; }

        public MerchantEntity? Merchant { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<DeviceEntity>? Devices { get; set; }
    }
}
