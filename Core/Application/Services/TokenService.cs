using Domain.Auth;
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
        private readonly JwtSettings _settings;

        public TokenService(JwtSettings settings) => _settings = settings;

        public string GenerateAccessToken(PlatformUserEntity user, IEnumerable<string> permissions)
        {
            var claims = BaseClaims(user.Id, user.PhoneNumber, UserGroup.Platform, user.Type.ToString(), permissions);

            // Merchant operator → o'z merchantiga scoped. Manage → claim yo'q (cheklovsiz).
            if (user.Type == PlatformUserType.Merchant && user.MerchantId.HasValue)
                claims.Add(new Claim("MerchantId", user.MerchantId.Value.ToString()));

            return Write(claims, JwtAudiences.Platform);
        }

        public string GenerateAccessToken(CustomerUserEntity user, IEnumerable<string> permissions)
        {
            var claims = BaseClaims(user.Id, user.PhoneNumber, UserGroup.Customer, user.Type.ToString(), permissions);

            // Corporate → tashkilot scopei. Natural → claim yo'q.
            if (user.Type == CustomerUserType.Corporate && user.OrganizationId.HasValue)
                claims.Add(new Claim("OrganizationId", user.OrganizationId.Value.ToString()));

            return Write(claims, JwtAudiences.Customer);
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

        private string Write(IEnumerable<Claim> claims, string audience)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                audience: audience,
                expires: DateTime.Now.AddMinutes(_settings.AccessTokenMinutes),
                claims: claims,
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
