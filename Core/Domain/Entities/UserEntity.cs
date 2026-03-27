using Domain.Entities.BaseEntity;
using Domain.Enums;

namespace Domain.Entities
{
    public class UserEntity : Entity
    {
        public required string PhoneId { get; set; }
        public required string Mail { get; set; }
        public required string PhoneNumber { get; set; }
        public decimal Balance { get; set; } = 0;
        public bool IsBlocked { get; set; } = false;
        public bool IsVerified { get; set; } = false;
        public bool IsOtpVerified { get; set; } = false;
        UserType UserType { get; set; } = UserType.NaturalPerson;
        public DateTime LastLoginDate { get; set; } = DateTime.Now;
        public DateTime LastActiveDate { get; set; } = DateTime.Now;
        public string? PasswordHash { get; set; }
        public string? PasswordSalt { get; set; }
    }
}
