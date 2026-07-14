using Domain.Enums;

namespace Domain.Constants
{
    /// <summary>
    /// Permissionlarning rol turi (<see cref="RoleKind"/>) bo'yicha biriktirilishi mumkinligini
    /// belgilaydi. Rol yaratish/yangilashda permission tanlovini cheklash uchun ishlatiladi.
    /// </summary>
    public static class PermissionScopes
    {
        /// <summary>
        /// Faqat Platform/Manage rolda bo'lishi mumkin permissionlar —
        /// platforma darajasidagi yaratish/o'chirish va global ko'rinishlar.
        /// Merchant rolga biriktirilmaydi.
        /// </summary>
        public static readonly HashSet<string> ManageOnly = new()
        {
            Permissions.OrganizationAdminCreate,
            Permissions.OrganizationAdminGetAll,
            Permissions.OrganizationAdminGetById,
            Permissions.OrganizationAdminUpdate,
            Permissions.OrganizationAdminDelete,

            Permissions.MerchantAdminRegister,
            Permissions.MerchantAdminDelete,
            Permissions.MerchantAdminGetAll,

            Permissions.BalanceTopUp,

            Permissions.PaymentAdminGetAll,
            Permissions.PaymentAdminReverse,

            // Hold invoice: moliyaviy operator amallari — faqat Manage (expert).
            Permissions.HoldAdminCapture,
            Permissions.HoldAdminRefund,
            Permissions.HoldAdminRetry,
            Permissions.MerchantAdminSetPaymeCredentials,
        };

        /// <summary>
        /// Customer/Natural (jismoniy) rol uchun ruxsat etilgan permissionlar —
        /// faqat mahsulot olish (sessiya/jarayon), o'z profili, o'z hisoboti, o'z balansini to'ldirish.
        /// </summary>
        public static readonly HashSet<string> NaturalAllowed = new()
        {
            Permissions.SessionCreate, Permissions.SessionClose,
            Permissions.SessionRead, Permissions.SessionHeartbeat,

            Permissions.ProcessStart, Permissions.ProcessStop,
            Permissions.ProcessPause, Permissions.ProcessResume,

            Permissions.UserMe, Permissions.UserUpdateMe,
            Permissions.UserBootstrap, Permissions.DeviceConnectionGetProducts,

            Permissions.ReportMyUsage, Permissions.ReportMyUsageExport,

            Permissions.PaymentTopUpSelf, Permissions.PaymentGetMyTransactions,

            Permissions.PaymentHoldCreate, Permissions.PaymentHoldRead,
            Permissions.PaymentHoldCancel,
        };

        /// <summary>
        /// Customer/Corporate rol uchun ruxsat etilgan permissionlar —
        /// Natural to'plami + tashkilot balansi/hisoboti + qo'l-osti userlarni boshqarish.
        /// </summary>
        public static readonly HashSet<string> CorporateAllowed = NaturalAllowed
            .Concat(new[]
            {
                Permissions.PaymentTopUpOrganization,
                Permissions.PaymentGetOrganizationTransactions,
                Permissions.OrganizationReportUsage,
                Permissions.OrganizationReportUsageExport,

                Permissions.CustomerAdminCreate, Permissions.CustomerAdminGetAll,
                Permissions.CustomerAdminGetById, Permissions.CustomerAdminSetPassword,
                Permissions.CustomerAdminBlock, Permissions.CustomerAdminUnblock,
                Permissions.CustomerAdminDelete,
            })
            .ToHashSet();

        /// <summary>
        /// Platform/Merchant rol uchun ruxsat etilgan permissionlar —
        /// barcha platform permissionlari, lekin <see cref="ManageOnly"/> bundan mustasno.
        /// </summary>
        public static readonly HashSet<string> MerchantAllowed = Permissions.PlatformAll
            .Where(p => !ManageOnly.Contains(p))
            .ToHashSet();

        /// <summary>
        /// Berilgan rol turi uchun permissionning ruxsat etilganligini qaytaradi.
        /// </summary>
        public static bool IsAllowedFor(RoleKind kind, string permission)
            => kind switch
            {
                // Manage — to'liq nazorat: barcha permissionlar (platform + customer).
                RoleKind.PlatformManage => true,
                RoleKind.PlatformMerchant => MerchantAllowed.Contains(permission),
                RoleKind.CustomerCorporate => CorporateAllowed.Contains(permission),
                RoleKind.CustomerNatural => NaturalAllowed.Contains(permission),
                _ => false
            };
    }
}
