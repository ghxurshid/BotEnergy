using Domain.Enums;

namespace Domain.Entities
{
    /// <summary>
    /// Platformani boshqaruvchi foydalanuvchi (jadval: auth.platform_users).
    /// <see cref="PlatformUserType.Manage"/> — scope cheklovi yo'q (butun platforma).
    /// <see cref="PlatformUserType.Merchant"/> — faqat o'z merchantiga tegishli elementlar.
    /// Self-register yo'q: bularni faqat Manage foydalanuvchi yaratadi.
    /// </summary>
    public class PlatformUserEntity : UserBase
    {
        public PlatformUserType Type { get; set; } = PlatformUserType.Manage;

        /// <summary>Faqat <see cref="PlatformUserType.Merchant"/> uchun to'ldiriladi; Manage uchun null.</summary>
        public long? MerchantId { get; set; }
        public MerchantEntity? Merchant { get; set; }

        public PlatformRoleEntity? Role { get; set; }
    }
}
