using System.Security.Claims;
using Domain.Auth;
using Domain.Enums;

namespace AdminApi.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static long GetUserId(this ClaimsPrincipal user)
            => long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public static UserType GetUserType(this ClaimsPrincipal user)
            => Enum.TryParse<UserType>(user.FindFirstValue("UserType"), out var type)
                ? type
                : UserType.NaturalPerson;

        public static bool IsPlatform(this ClaimsPrincipal user)
            => user.GetUserType() == UserType.Platform;

        /// <summary>JWT claimlaridan caller'ning to'liq ruxsat doirasini quradi (DB'siz).</summary>
        public static AccessScope GetScope(this ClaimsPrincipal user)
            => new AccessScope(
                UserId: user.GetUserId(),
                UserType: user.GetUserType(),
                MerchantId: user.GetMerchantId(),
                OrganizationId: user.GetOrganizationId(),
                StationId: user.GetStationId(),
                Permissions: user.GetPermissions());

        public static HashSet<string> GetPermissions(this ClaimsPrincipal user)
            => user.Claims
                .Where(c => c.Type == "Permission")
                .Select(c => c.Value)
                .ToHashSet();

        /// <summary>Token'dagi scope claimlari — user turiga qarab mavjud bo'lishi mumkin (yo'q bo'lsa null).</summary>
        public static long? GetMerchantId(this ClaimsPrincipal user) => ParseLongClaim(user, "MerchantId");
        public static long? GetStationId(this ClaimsPrincipal user) => ParseLongClaim(user, "StationId");
        public static long? GetOrganizationId(this ClaimsPrincipal user) => ParseLongClaim(user, "OrganizationId");

        private static long? ParseLongClaim(ClaimsPrincipal user, string claimType)
            => long.TryParse(user.FindFirstValue(claimType), out var value) ? value : null;
    }
}
