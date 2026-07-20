using Domain.Enums;

namespace Domain.Dtos
{
    /// <summary>Platform foydalanuvchi (Manage/Merchant) yaratish.</summary>
    public class CreateUserAdminDto
    {
        public required string PhoneId { get; set; }
        public required string Mail { get; set; }
        public required string PhoneNumber { get; set; }
        public long RoleId { get; set; }

        /// <summary>Manage yoki Merchant.</summary>
        public PlatformUserType Type { get; set; } = PlatformUserType.Manage;

        /// <summary>Merchant subtipi uchun majburiy; Manage uchun e'tiborsiz.</summary>
        public long? MerchantId { get; set; }
    }

    /// <summary>Corporate (tashkilot) foydalanuvchi yaratish.</summary>
    public class CreateCorporateUserDto
    {
        public required string PhoneId { get; set; }
        public required string Mail { get; set; }
        public required string PhoneNumber { get; set; }
        public long RoleId { get; set; }
        public long OrganizationId { get; set; }
    }

    public class SetPasswordAdminDto
    {
        public long UserId { get; set; }
        public required string Password { get; set; }

        /// <summary>
        /// Amalni bajarayotgan admin (caller)ning JORIY paroli. Platform userlariga parol
        /// o'rnatishda majburiy — backend caller'ning o'z parolini tekshiradi.
        /// </summary>
        public string? CurrentPassword { get; set; }
    }

    public class ResetPasswordAdminDto
    {
        public long UserId { get; set; }
        public required string NewPassword { get; set; }

        /// <summary>
        /// Amalni bajarayotgan admin (caller)ning JORIY paroli. Platform userlarining parolini
        /// reset qilishda majburiy — backend caller'ning o'z parolini tekshiradi.
        /// </summary>
        public string? CurrentPassword { get; set; }
    }

    /// <summary>Joriy (login qilgan) platform userning o'z profili.</summary>
    public class MyProfileDto
    {
        public long Id { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string Mail { get; set; } = string.Empty;
        public string SubType { get; set; } = string.Empty;
        public long? MerchantId { get; set; }
        public long? RoleId { get; set; }
        public string? RoleName { get; set; }
        public bool IsVerified { get; set; }
        public bool IsBlocked { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastLoginDate { get; set; }
    }

    /// <summary>Platform foydalanuvchi ro'yxat elementi.</summary>
    public class UserAdminItemDto
    {
        public long Id { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string Mail { get; set; } = string.Empty;
        public string SubType { get; set; } = string.Empty;
        public long? MerchantId { get; set; }
        public bool IsVerified { get; set; }
        public bool IsBlocked { get; set; }
        public long? RoleId { get; set; }
        public string? RoleName { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastLoginDate { get; set; }
    }

    /// <summary>Customer (corporate) foydalanuvchi ro'yxat elementi.</summary>
    public class CustomerUserItemDto
    {
        public long Id { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string Mail { get; set; } = string.Empty;
        public string SubType { get; set; } = string.Empty;
        public long? OrganizationId { get; set; }
        public decimal Balance { get; set; }
        public bool IsVerified { get; set; }
        public bool IsBlocked { get; set; }
        public long? RoleId { get; set; }
        public string? RoleName { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastLoginDate { get; set; }
    }

    public class UserAdminResultDto
    {
        public long Id { get; set; }
        public required string ResultMessage { get; set; }
    }
}
