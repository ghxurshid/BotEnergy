using Domain.Attributes;
using Domain.Entities.BaseEntity;

namespace Domain.Entities
{
    /// <summary>
    /// Barcha foydalanuvchi turlari uchun umumiy maydonlar (autentifikatsiya, holat).
    /// TPH emas — bu mapped bo'lmagan baza; har konkret entity (<see cref="PlatformUserEntity"/>,
    /// <see cref="CustomerUserEntity"/>) o'z jadvaliga map qilinadi.
    /// </summary>
    public abstract class UserBase : Entity
    {
        public required string PhoneId { get; set; }
        public required string Mail { get; set; }
        public required string PhoneNumber { get; set; }
        public bool IsBlocked { get; set; } = false;
        public bool IsVerified { get; set; } = false;
        public bool IsOtpVerified { get; set; } = false;
        public long? RoleId { get; set; }
        public DateTime LastLoginDate { get; set; } = DateTime.Now;
        public DateTime LastActiveDate { get; set; } = DateTime.Now;
        [NotSearchable]
        public string? PasswordHash { get; set; }
        [NotSearchable]
        public string? PasswordSalt { get; set; }
    }
}
