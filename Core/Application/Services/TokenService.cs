using Domain.Entities;
using Domain.Interfaces;
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;


namespace Application.Services
{
    public class TokenService : ITokenService
    {
        private const string SECRET = "3f1e2d4c5a6b7c8d9e0f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d";

        public string GenerateAccessToken(UserEntity user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.PhoneNumber),                          
            };

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
