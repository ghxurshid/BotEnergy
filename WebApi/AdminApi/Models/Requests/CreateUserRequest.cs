using Domain.Enums;

namespace AdminApi.Models.Requests
{
    /// <summary>Platform foydalanuvchi (Manage/Merchant) yaratish so'rovi.</summary>
    public class CreateUserRequest
    {
        public string PhoneId { get; set; } = string.Empty;
        public string Mail { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public long RoleId { get; set; }

        /// <summary>Manage yoki Merchant.</summary>
        public PlatformUserType Type { get; set; } = PlatformUserType.Manage;

        /// <summary>Merchant subtipi uchun majburiy.</summary>
        public long? MerchantId { get; set; }
    }
}
