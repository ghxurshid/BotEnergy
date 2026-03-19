using Domain.Entities.BaseEntity;

namespace Domain.Entities
{
    public class UsageSessionEntity : Entity
    { 
        public long UserId { get; set; }

        public UserEntity? User { get; set; }

        public long DeviceId { get; set; }

        public DeviceEntity? Device { get; set; }

        public string ProductType { get; set; } = string.Empty;

        public decimal Quantity { get; set; }

        public decimal Price { get; set; }

        public DateTime StartedAt { get; set; } = DateTime.Now;

        public DateTime? EndedAt { get; set; }
    }
}
