using Domain.Enums;

namespace Domain.Dtos
{
    public class CreateUserAdminDto
    {
        public required string PhoneId { get; set; }
        public required string Mail { get; set; }
        public required string PhoneNumber { get; set; }
        public long RoleId { get; set; }
        public long? OrganizationId { get; set; }
        public long? StationId { get; set; }
    }

    public class SetPasswordAdminDto
    {
        public long UserId { get; set; }
        public required string Password { get; set; }
    }

    public class ResetPasswordAdminDto
    {
        public long UserId { get; set; }
        public required string NewPassword { get; set; }
    }

    public class UserAdminItemDto
    {
        public long Id { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string Mail { get; set; } = string.Empty;
        public UserType UserType { get; set; }
        public bool IsVerified { get; set; }
        public bool IsBlocked { get; set; }
        public long? RoleId { get; set; }
        public string? RoleName { get; set; }
        public decimal Balance { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastLoginDate { get; set; }
    }

    public class UserAdminResultDto
    {
        public long Id { get; set; }
        public required string ResultMessage { get; set; }
    }
}
