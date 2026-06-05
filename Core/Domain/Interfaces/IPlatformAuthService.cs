using Domain.Dtos;
using Domain.Dtos.Base;

namespace Domain.Interfaces
{
    /// <summary>
    /// Platform (Manage/Merchant) autentifikatsiyasi. Self-register yo'q —
    /// foydalanuvchilarni Manage yaratadi va parol o'rnatadi.
    /// </summary>
    public interface IPlatformAuthService
    {
        Task<GenericDto<LoginResultDto>> LoginAsync(LoginDto request);
        Task<GenericDto<RefreshTokenResultDto>> RefreshTokenAsync(RefreshTokenDto request);
    }
}
