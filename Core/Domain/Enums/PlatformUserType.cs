namespace Domain.Enums
{
    /// <summary>
    /// Platform guruhidagi foydalanuvchining subtipi.
    /// </summary>
    public enum PlatformUserType
    {
        /// <summary>Platformani ishlab chiquvchi/boshqaruvchi — scope cheklovi yo'q.</summary>
        Manage = 0,

        /// <summary>Biznes egasi/operatori — faqat o'z merchantiga tegishli elementlar.</summary>
        Merchant = 1
    }
}
