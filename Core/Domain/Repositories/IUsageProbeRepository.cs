namespace Domain.Repositories
{
    /// <summary>
    /// "Bu obyekt hozir band/ishlatilmoqdami?" savollariga javob beradigan yagona repozitoriy.
    ///
    /// Nega alohida: o'chirish va nofaollashtirish amallarining to'sqinlik omillari deyarli
    /// har doim BOSHQA jadvaldagi holatga bog'liq (stansiyani o'chirish — qurilmalari bormi,
    /// rolni o'chirish — kimgadir biriktirilganmi). Bu tekshiruvlarni har bir repozitoriyga
    /// tarqatib yuborish o'rniga bir joyda saqlaymiz: shunda "nimani tekshirmadik" savoliga
    /// javob bitta faylda ko'rinadi.
    ///
    /// Barcha metodlar faqat O'QIYDI va soft-delete filtridan o'tadi (o'chirilgan yozuv band qilmaydi).
    /// </summary>
    public interface IUsageProbeRepository
    {
        // ── Qurilma ───────────────────────────────────────────────

        /// <summary>Qurilmada yopilmagan foydalanuvchi sessiyasi bormi.</summary>
        Task<bool> DeviceHasActiveSessionAsync(long deviceId);

        /// <summary>Qurilmada tugallanmagan naqd → karta sessiyasi bormi.</summary>
        Task<bool> DeviceHasOpenCashSessionAsync(long deviceId);

        /// <summary>Qurilmada yakunlanmagan inkassatsiya (Requested/BoxOpened) bormi.</summary>
        Task<bool> DeviceHasOpenCollectionAsync(long deviceId);

        // ── Stansiya ──────────────────────────────────────────────

        Task<int> StationDeviceCountAsync(long stationId);
        Task<bool> StationHasActiveSessionAsync(long stationId);

        // ── Merchant ──────────────────────────────────────────────

        Task<int> MerchantStationCountAsync(long merchantId);

        /// <summary>Merchantga biriktirilgan platform operatorlari soni.</summary>
        Task<int> MerchantOperatorCountAsync(long merchantId);

        Task<bool> MerchantHasActiveSessionAsync(long merchantId);

        // ── Tashkilot ─────────────────────────────────────────────

        Task<int> OrganizationUserCountAsync(long organizationId);

        // ── Mahsulot ──────────────────────────────────────────────

        /// <summary>Mahsulot hozir tugallanmagan jarayonda ishlatilmoqdami.</summary>
        Task<bool> ProductHasActiveProcessAsync(long productId);

        // ── Rol ───────────────────────────────────────────────────

        Task<int> PlatformRoleUserCountAsync(long roleId);
        Task<int> CustomerRoleUserCountAsync(long roleId);

        // ── Foydalanuvchi ─────────────────────────────────────────

        Task<bool> CustomerUserHasActiveSessionAsync(long userId);

        /// <summary>
        /// Bloklanmagan Manage administratorlar soni (ixtiyoriy ravishda bittasini hisobga olmasdan) —
        /// "oxirgi adminni o'chirib qo'yish" holatining oldini olish uchun.
        /// </summary>
        Task<int> ActiveManageUserCountAsync(long? excludeUserId = null);
    }
}
