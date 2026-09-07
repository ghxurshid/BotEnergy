using System.Security.Claims;
using Domain.Auth;
using Domain.Enums;

namespace CommonConfiguration.Extensions
{
    /// <summary>
    /// JWT claimlaridan caller ma'lumotlarini o'qishning yagona nuqtasi (barcha API uchun).
    ///
    /// Nega bitta joyda: bu — xavfsizlik yuzasi. Permission "nima qila oladi"ni, bu yerda
    /// quriladigan <see cref="AccessScope"/> esa "qaysi ma'lumot ustida"ni belgilaydi.
    /// Har API o'z nusxasini saqlasa, bittasidagi tuzatish qolganlariga yetmaydi.
    ///
    /// <c>ClaimsPrincipal</c> nullable qabul qilinadi — SignalR hub'ida <c>Context.User</c>
    /// shunday. Claim yo'q yoki buzuq bo'lsa istisno tashlanmaydi, scope shunchaki bo'sh
    /// quriladi: <c>IsManage</c>/<c>CanAccess*</c> false qaytaradi, ya'ni hech nimaga ruxsat bermaydi.
    /// </summary>
    public static class ClaimsPrincipalExtensions
    {
        /// <summary>
        /// Autentifikatsiyadan o'tgan endpoint uchun. Claim bo'lmasa istisno — <c>[Authorize]</c>
        /// ortida bu holat konfiguratsiya xatosi, jimgina 0 bilan davom etish xavfliroq.
        /// </summary>
        public static long GetUserId(this ClaimsPrincipal user)
            => long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>Xavfsiz variant — hub va autentifikatsiya ixtiyoriy bo'lgan joylar uchun.</summary>
        public static long? GetUserIdOrNull(this ClaimsPrincipal? user)
            => long.TryParse(user?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        /// <summary>Noma'lum qiymatda Customer — ikki guruhdan kamroq huquqlisi (fail-safe).</summary>
        public static UserGroup GetUserGroup(this ClaimsPrincipal? user)
            => Enum.TryParse<UserGroup>(user?.FindFirstValue("UserGroup"), out var g)
                ? g
                : UserGroup.Customer;

        public static string GetSubType(this ClaimsPrincipal? user)
            => user?.FindFirstValue("UserSubType") ?? string.Empty;

        public static bool IsManage(this ClaimsPrincipal? user)
            => user.GetUserGroup() == UserGroup.Platform
               && string.Equals(user.GetSubType(), nameof(PlatformUserType.Manage), StringComparison.OrdinalIgnoreCase);

        /// <summary>JWT claimlaridan caller'ning to'liq ruxsat doirasini quradi (DB'siz).</summary>
        public static AccessScope GetScope(this ClaimsPrincipal? user)
            => new AccessScope(
                UserId: user.GetUserIdOrNull() ?? 0,
                Group: user.GetUserGroup(),
                SubType: user.GetSubType(),
                MerchantId: user.GetMerchantId(),
                OrganizationId: user.GetOrganizationId(),
                Permissions: user.GetPermissions());

        public static HashSet<string> GetPermissions(this ClaimsPrincipal? user)
            => user?.Claims
                   .Where(c => c.Type == "Permission")
                   .Select(c => c.Value)
                   .ToHashSet()
               ?? new HashSet<string>();

        public static long? GetMerchantId(this ClaimsPrincipal? user) => ParseLongClaim(user, "MerchantId");

        public static long? GetOrganizationId(this ClaimsPrincipal? user) => ParseLongClaim(user, "OrganizationId");

        private static long? ParseLongClaim(ClaimsPrincipal? user, string claimType)
            => long.TryParse(user?.FindFirstValue(claimType), out var value) ? value : null;
    }
}
