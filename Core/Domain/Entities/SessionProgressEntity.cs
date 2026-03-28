using Domain.Entities.BaseEntity;

namespace Domain.Entities
{
    public class SessionProgressEntity : Entity
    {
        public long SessionId { get; set; }
        public UsageSessionEntity? Session { get; set; }

        public decimal Quantity { get; set; }
        public decimal TotalQuantity { get; set; }
        public DateTime ReportedAt { get; set; } = DateTime.Now;
    }
}
