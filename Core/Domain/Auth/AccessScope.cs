using Domain.Enums;

namespace Domain.Auth
{
    /// <summary>
    /// Caller'ning ruxsat doirasi (scope) — JWT claimlaridan tuziladi.
    /// Permission tekshiruvidan ALOHIDA: permission "nima qila oladi"ni, scope esa
    /// "qaysi ma'lumot ustida"ni belgilaydi.
    ///
    /// Prioritet:
    ///  - Platform  → scope cheklovi yo'q (permissioni bo'lsa hammasi).
    ///  - Merchant  → faqat o'z merchanti (MerchantId).
    ///  - Legal     → faqat o'z tashkiloti (OrganizationId).
    ///  - Natural   → admin entity'lariga scope yo'q (faqat mahsulot + report).
    /// </summary>
    public sealed record AccessScope(
        long UserId,
        UserType UserType,
        long? MerchantId,
        long? OrganizationId,
        long? StationId,
        IReadOnlySet<string> Permissions)
    {
        public bool IsPlatform => UserType == UserType.Platform;

        public bool HasPermission(string permission) => Permissions.Contains(permission);

        /// <summary>Platform → har doim; Merchant user → faqat o'z merchanti; aks holda false.</summary>
        public bool CanAccessMerchant(long merchantId)
            => IsPlatform || (MerchantId.HasValue && MerchantId.Value == merchantId);

        /// <summary>Platform → har doim; Legal user → faqat o'z tashkiloti; aks holda false.</summary>
        public bool CanAccessOrganization(long organizationId)
            => IsPlatform || (OrganizationId.HasValue && OrganizationId.Value == organizationId);
    }
}
