using Domain.Entities.BaseEntity;
using Domain.Enums;

namespace Domain.Entities
{
    public abstract class UserEntity : Entity
    {
        public abstract UserType UserType { get; }
        public required string PhoneId { get; set; }
        public required string Mail { get; set; }
        public required string PhoneNumber { get; set; }
        public bool IsBlocked { get; set; } = false;
        public bool IsVerified { get; set; } = false;
        public bool IsOtpVerified { get; set; } = false;
        public long? RoleId { get; set; }
        public RoleEntity? Role { get; set; }
        public DateTime LastLoginDate { get; set; } = DateTime.Now;
        public DateTime LastActiveDate { get; set; } = DateTime.Now;
        public string? PasswordHash { get; set; }
        public string? PasswordSalt { get; set; }

        public ICollection<UserRoleEntity>? UserRoles { get; set; }
        public ICollection<UsageSessionEntity>? UsageSessions { get; set; }
    }
}
