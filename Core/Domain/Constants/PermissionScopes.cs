using Domain.Enums;

namespace Domain.Constants
{
    /// <summary>
    /// Permissionlarning scope (kontekst) bo'yicha tasniflanishi.
    /// Role scopega biriktirilganda — shu rolga qaysi permissionlarni
    /// biriktirish mumkinligini aniqlaydi.
    /// </summary>
    public static class PermissionScopes
    {
        /// <summary>
        /// Faqat global (NaturalRole) rolda bo'lishi kerak permissionlar —
        /// platforma darajasidagi entitylarni yaratish/o'chirish.
        /// </summary>
        public static readonly HashSet<string> GlobalOnly = new()
        {
            Permissions.MerchantAdminRegister,
            Permissions.MerchantAdminDelete,
            Permissions.OrganizationAdminCreate,
            Permissions.OrganizationAdminDelete,
            Permissions.YuridikAdminCreate
        };

        /// <summary>
        /// Merchant scope ichidagi permissionlar (Merchant → Station → Device → Product).
        /// Organization roliga biriktirilmaydi.
        /// </summary>
        public static readonly HashSet<string> MerchantScope = new()
        {
            Permissions.MerchantAdminGetAll,
            Permissions.MerchantAdminGetById,
            Permissions.MerchantAdminUpdate,

            Permissions.StationAdminCreate,
            Permissions.StationAdminGetAll,
            Permissions.StationAdminGetById,
            Permissions.StationAdminGetByMerchant,
            Permissions.StationAdminUpdate,
            Permissions.StationAdminDelete,

            Permissions.DeviceAdminRegister,
            Permissions.DeviceAdminGetAll,
            Permissions.DeviceAdminGetById,
            Permissions.DeviceAdminGetByStation,
            Permissions.DeviceAdminUpdate,
            Permissions.DeviceAdminDelete,

            Permissions.ProductAdminCreate,
            Permissions.ProductAdminGetAll,
            Permissions.ProductAdminGetByDevice,
            Permissions.ProductAdminGetById,
            Permissions.ProductAdminGetAllowedTypes,
            Permissions.ProductAdminUpdate,
            Permissions.ProductAdminDelete
        };

        /// <summary>
        /// Organization scope ichidagi permissionlar.
        /// Merchant roliga biriktirilmaydi.
        /// </summary>
        public static readonly HashSet<string> OrganizationScope = new()
        {
            Permissions.OrganizationAdminGetAll,
            Permissions.OrganizationAdminGetById,
            Permissions.OrganizationAdminUpdate,

            Permissions.BalanceGetMyBalance,
            Permissions.BalanceTopUp
        };

        /// <summary>
        /// Berilgan rol turi uchun permissionning ruxsat etilganligini qaytaradi.
        /// </summary>
        public static bool IsAllowedFor(RoleType roleType, string permission)
            => roleType switch
            {
                RoleType.NaturalRole => true,
                RoleType.LegalRole =>
                    !GlobalOnly.Contains(permission) && !MerchantScope.Contains(permission),
                RoleType.MerchantRole =>
                    !GlobalOnly.Contains(permission) && !OrganizationScope.Contains(permission),
                _ => false
            };
    }
}
