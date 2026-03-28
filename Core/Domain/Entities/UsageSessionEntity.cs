using Domain.Entities.BaseEntity;

namespace Domain.Entities
{
    public enum SessionStatus
    {
        Pending,
        DeviceConnected,
        InProgress,
        Completed,
        ClosedByUser,
        TimedOut
    }

    public class UsageSessionEntity : Entity
    {
        public long UserId { get; set; }
        public UserEntity? User { get; set; }

        public long? DeviceId { get; set; }
        public DeviceEntity? Device { get; set; }

        public string SessionToken { get; set; } = string.Empty;
        public SessionStatus Status { get; set; } = SessionStatus.Pending;

        public string ProductType { get; set; } = string.Empty;
        public decimal? RequestedQuantity { get; set; }
        public decimal DeliveredQuantity { get; set; } = 0;
        public decimal Price { get; set; } = 0;

        public DateTime StartedAt { get; set; } = DateTime.Now;
        public DateTime? DeviceConnectedAt { get; set; }
        public DateTime? LastActivityAt { get; set; } = DateTime.Now;
        public DateTime? EndedAt { get; set; }
    }
}
