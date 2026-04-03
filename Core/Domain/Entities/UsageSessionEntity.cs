using Domain.Entities.BaseEntity;
using Domain.Enums;

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
        // ── FK lar ──────────────────────────────────────────────────────
        public long UserId { get; set; }
        public UserEntity? User { get; set; }

        public long? DeviceId { get; set; }
        public DeviceEntity? Device { get; set; }

        public long? ProductId { get; set; }
        public ProductEntity? Product { get; set; }

        // ── Snapshot maydonlar (tarixiy ma'lumot saqlanishi uchun) ─────
        public string UserPhoneNumber { get; set; } = string.Empty;
        public string DeviceSerialNumber { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal PricePerUnit { get; set; }

        // ── Sessiya holati ────────────────────────────────────────────
        public string SessionToken { get; set; } = string.Empty;
        public SessionStatus Status { get; set; } = SessionStatus.Pending;

        public UnitType? Unit { get; set; }
        public decimal? RequestedQuantity { get; set; }
        public decimal DeliveredQuantity { get; set; } = 0;

        public string? EndReason { get; set; }

        public DateTime StartedAt { get; set; } = DateTime.Now;
        public DateTime? DeviceConnectedAt { get; set; }
        public DateTime? LastActivityAt { get; set; } = DateTime.Now;
        public DateTime? EndedAt { get; set; }
    }
}
