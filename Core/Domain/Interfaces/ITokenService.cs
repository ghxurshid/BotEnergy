using Domain.Entities;

namespace Domain.Interfaces
{
    public interface ITokenService
    {
        string GenerateAccessToken(PlatformUserEntity user, IEnumerable<string> permissions);
        string GenerateAccessToken(CustomerUserEntity user, IEnumerable<string> permissions);
        string GenerateRefreshToken();
    }
}
