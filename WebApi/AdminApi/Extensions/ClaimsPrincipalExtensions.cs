using System.Security.Claims;

namespace AdminApi.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static long GetUserId(this ClaimsPrincipal user)
            => long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public static HashSet<string> GetPermissions(this ClaimsPrincipal user)
            => user.Claims
                .Where(c => c.Type == "Permission")
                .Select(c => c.Value)
                .ToHashSet();
    }
}
