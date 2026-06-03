using Domain.Entities;
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

        public string GenerateAccessToken(UserEntity user, IEnumerable<string> permissions)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.PhoneNumber),
                new Claim("UserType", user.UserType.ToString()),
            };

            foreach (var permission in permissions)
                claims.Add(new Claim("Permission", permission));

            // Scope claimlari — user turiga qarab faqat mavjud bo'lganlari qo'shiladi.
            // MerchantUser: StationId (to'g'ridan-to'g'ri) + MerchantId (Station orqali, agar yuklangan bo'lsa).
            // LegalUser: OrganizationId. NaturalUser (mobil): hech biri.
            switch (user)
            {
                case MerchantUserEntity merchantUser:
                    claims.Add(new Claim("StationId", merchantUser.StationId.ToString()));
                    if (merchantUser.Station is not null)
                        claims.Add(new Claim("MerchantId", merchantUser.Station.MerchantId.ToString()));
                    break;
                case LegalUserEntity legalUser when legalUser.OrganizationId.HasValue:
                    claims.Add(new Claim("OrganizationId", legalUser.OrganizationId.Value.ToString()));
                    break;
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SECRET));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                expires: DateTime.Now.AddMinutes(15),
                claims: claims,
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            return Guid.NewGuid().ToString();
        }
    }
}
