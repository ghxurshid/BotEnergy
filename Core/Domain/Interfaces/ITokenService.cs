using Domain.Entities;

namespace Domain.Interfaces
{
    public interface ITokenService
    {
        string GenerateAccessToken(UserEntity user);
        string GenerateRefreshToken();
    }
}
