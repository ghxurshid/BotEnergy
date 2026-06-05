using System.Security.Claims;
using Domain.Auth;
using Domain.Enums;

namespace AdminApi.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static long GetUserId(this ClaimsPrincipal user)
            => long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public static UserGroup GetUserGroup(this ClaimsPrincipal user)
            => Enum.TryParse<UserGroup>(user.FindFirstValue("UserGroup"), out var g)
                ? g
                : UserGroup.Customer;

        public static string GetSubType(this ClaimsPrincipal user)
            => user.FindFirstValue("UserSubType") ?? string.Empty;

        public static bool IsManage(this ClaimsPrincipal user)
            => user.GetUserGroup() == UserGroup.Platform
               && string.Equals(user.GetSubType(), nameof(PlatformUserType.Manage), StringComparison.OrdinalIgnoreCase);

        /// <summary>JWT claimlaridan caller'ning to'liq ruxsat doirasini quradi (DB'siz).</summary>
        public static AccessScope GetScope(this ClaimsPrincipal user)
            => new AccessScope(
                UserId: user.GetUserId(),
                Group: user.GetUserGroup(),
                SubType: user.GetSubType(),
                MerchantId: user.GetMerchantId(),
                OrganizationId: user.GetOrganizationId(),
                Permissions: user.GetPermissions());

        public static HashSet<string> GetPermissions(this ClaimsPrincipal user)
            => user.Claims
                .Where(c => c.Type == "Permission")
                .Select(c => c.Value)
                .ToHashSet();

        public static long? GetMerchantId(this ClaimsPrincipal user) => ParseLongClaim(user, "MerchantId");
        public static long? GetOrganizationId(this ClaimsPrincipal user) => ParseLongClaim(user, "OrganizationId");

        private static long? ParseLongClaim(ClaimsPrincipal user, string claimType)
            => long.TryParse(user.FindFirstValue(claimType), out var value) ? value : null;
    }
}
