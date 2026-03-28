using Domain.Entities;

namespace Domain.Interfaces
{
    public interface ITokenService
    {
        string GenerateAccessToken(UserEntity user, IEnumerable<string> permissions);
        string GenerateRefreshToken();
    }
}
