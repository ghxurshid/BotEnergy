using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Application.Services
{
    public class TokenService : ITokenService
    {
        private const string SECRET = "3f1e2d4c5a6b7c8d9e0f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d";

        public string GenerateAccessToken(PlatformUserEntity user, IEnumerable<string> permissions)
        {
            var claims = BaseClaims(user.Id, user.PhoneNumber, UserGroup.Platform, user.Type.ToString(), permissions);

            // Merchant operator → o'z merchantiga scoped. Manage → claim yo'q (cheklovsiz).
            if (user.Type == PlatformUserType.Merchant && user.MerchantId.HasValue)
                claims.Add(new Claim("MerchantId", user.MerchantId.Value.ToString()));

            return Write(claims);
        }

        public string GenerateAccessToken(CustomerUserEntity user, IEnumerable<string> permissions)
        {
            var claims = BaseClaims(user.Id, user.PhoneNumber, UserGroup.Customer, user.Type.ToString(), permissions);

            // Corporate → tashkilot scopei. Natural → claim yo'q.
            if (user.Type == CustomerUserType.Corporate && user.OrganizationId.HasValue)
                claims.Add(new Claim("OrganizationId", user.OrganizationId.Value.ToString()));

            return Write(claims);
        }

        public string GenerateRefreshToken() => Guid.NewGuid().ToString();

        private static List<Claim> BaseClaims(long id, string phone, UserGroup group, string subType, IEnumerable<string> permissions)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, id.ToString()),
                new(ClaimTypes.Name, phone),
                new("UserGroup", group.ToString()),
                new("UserSubType", subType),
            };

            foreach (var permission in permissions)
                claims.Add(new Claim("Permission", permission));

            return claims;
        }

        private static string Write(IEnumerable<Claim> claims)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SECRET));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                expires: DateTime.Now.AddMinutes(15),
                claims: claims,
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
