using Domain.Enums;

namespace Domain.Auth
{
    /// <summary>
    /// Caller'ning ruxsat doirasi (scope) — JWT claimlaridan tuziladi.
    /// Permission tekshiruvidan ALOHIDA: permission "nima qila oladi"ni, scope esa
    /// "qaysi ma'lumot ustida"ni belgilaydi.
    ///
    /// Prioritet:
    ///  - Platform/Manage   → scope cheklovi yo'q (permissioni bo'lsa hammasi).
    ///  - Platform/Merchant → faqat o'z merchanti (MerchantId).
    ///  - Customer/Corporate→ faqat o'z tashkiloti (OrganizationId).
    ///  - Customer/Natural  → admin entity'lariga scope yo'q (faqat mahsulot + report).
    /// </summary>
    public sealed record AccessScope(
        long UserId,
        UserGroup Group,
        string SubType,
        long? MerchantId,
        long? OrganizationId,
        IReadOnlySet<string> Permissions)
    {
        public bool IsPlatform => Group == UserGroup.Platform;
        public bool IsCustomer => Group == UserGroup.Customer;

        /// <summary>Platform/Manage — cheklovsiz to'liq kirish.</summary>
        public bool IsManage => Group == UserGroup.Platform
            && string.Equals(SubType, nameof(PlatformUserType.Manage), StringComparison.OrdinalIgnoreCase);

        /// <summary>Platform/Merchant — bitta merchantga scoped operator.</summary>
        public bool IsMerchant => Group == UserGroup.Platform
            && string.Equals(SubType, nameof(PlatformUserType.Merchant), StringComparison.OrdinalIgnoreCase);

        public bool IsCorporate => Group == UserGroup.Customer
            && string.Equals(SubType, nameof(CustomerUserType.Corporate), StringComparison.OrdinalIgnoreCase);

        public bool HasPermission(string permission) => Permissions.Contains(permission);

        /// <summary>Manage → har doim; Merchant → faqat o'z merchanti; aks holda false.</summary>
        public bool CanAccessMerchant(long merchantId)
            => IsManage || (MerchantId.HasValue && MerchantId.Value == merchantId);

        /// <summary>Manage → har doim; Corporate → faqat o'z tashkiloti; aks holda false.</summary>
        public bool CanAccessOrganization(long organizationId)
            => IsManage || (OrganizationId.HasValue && OrganizationId.Value == organizationId);
    }
}
