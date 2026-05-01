using Domain.Entities.BaseEntity;
using Domain.Enums;

namespace Domain.Entities
{
    /// <summary>
    /// Foydalanuvchi sessiyasi — qurilma bilan bog'lanish va undan xizmat olish davri.
    /// Bir sessiya ichida ketma-ket bir nechta <see cref="ProductProcessEntity"/> bo'lishi mumkin
    /// (masalan: avval moyka, keyin chang yutgich).
    /// </summary>
    public class SessionEntity : Entity
    {
        public long UserId { get; set; }
        public UserEntity? User { get; set; }

        public long? DeviceId { get; set; }
        public DeviceEntity? Device { get; set; }

        public string SessionToken { get; set; } = string.Empty;
        public SessionStatus Status { get; set; } = SessionStatus.Created;
        public SessionCloseReason? CloseReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ConnectedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime LastActivityAt { get; set; } = DateTime.Now;

        public ICollection<ProductProcessEntity>? Processes { get; set; }
    }
}
