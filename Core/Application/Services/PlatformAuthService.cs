using Application.Helpers;
using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    /// <summary>
    /// Platform (Manage/Merchant) login va refresh. Refresh token qiymati "p:" prefiksi bilan
    /// saqlanadi (Customer tokenlaridan ajratish uchun).
    /// </summary>
    public class PlatformAuthService : IPlatformAuthService
    {
        private readonly IPlatformUserRepository _userRepository;
        private readonly ITokenService _tokenService;
        private readonly IPlatformRoleRepository _roleRepository;
        private readonly IRefreshTokenStore _refreshTokenStore;

        private const string TokenPrefix = "p:";
        private static readonly TimeSpan RefreshTokenExpiry = TimeSpan.FromDays(7);

        public PlatformAuthService(
            IPlatformUserRepository userRepository,
            ITokenService tokenService,
            IPlatformRoleRepository roleRepository,
            IRefreshTokenStore refreshTokenStore)
        {
            _userRepository = userRepository;
            _tokenService = tokenService;
            _roleRepository = roleRepository;
            _refreshTokenStore = refreshTokenStore;
        }

        public async Task<GenericDto<LoginResultDto>> LoginAsync(LoginDto request)
        {
            var user = await _userRepository.GetByPhoneNumberAsync(request.PhoneNumber);
            if (user is null)
                return GenericDto<LoginResultDto>.Error(404, "Telefon raqam yoki parol noto'g'ri.");

            if (user.PasswordHash is null)
                return GenericDto<LoginResultDto>.Error(403, "Parol o'rnatilmagan. Administrator bilan bog'laning.");

            var isPasswordValid = PasswordHelper.Verify(request.Password, user.PasswordHash, user.PasswordSalt!);
            if (!isPasswordValid)
                return GenericDto<LoginResultDto>.Error(401, "Telefon raqam yoki parol noto'g'ri.");

            if (!user.IsVerified)
                return GenericDto<LoginResultDto>.Error(403, "Akkaunt tasdiqlanmagan.");

            if (user.IsBlocked)
                return GenericDto<LoginResultDto>.Error(403, "Akkaunt bloklangan.");

            user.LastLoginDate = DateTime.Now;
            user.LastActiveDate = DateTime.Now;
            await _userRepository.UpdateAsync(user);

            var tokens = await GenerateAndPersistTokensAsync(user.Id, user.RoleId);

            return GenericDto<LoginResultDto>.Success(new LoginResultDto
            {
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
                AccessTokenExpiration = tokens.AccessTokenExpiration
            });
        }

        public async Task<GenericDto<RefreshTokenResultDto>> RefreshTokenAsync(RefreshTokenDto request)
        {
            if (!request.RefreshToken.StartsWith(TokenPrefix, StringComparison.Ordinal))
                return GenericDto<RefreshTokenResultDto>.Error(401, "Refresh token noto'g'ri yoki muddati o'tgan.");

            var userId = await _refreshTokenStore.GetUserIdAsync(request.RefreshToken);
            if (userId is null)
                return GenericDto<RefreshTokenResultDto>.Error(401, "Refresh token noto'g'ri yoki muddati o'tgan.");

            var user = await _userRepository.GetByIdAsync(userId.Value);
            if (user is null)
                return GenericDto<RefreshTokenResultDto>.Error(401, "Foydalanuvchi topilmadi.");

            if (user.IsBlocked)
                return GenericDto<RefreshTokenResultDto>.Error(403, "Akkaunt bloklangan.");

            if (user.IsDeleted)
                return GenericDto<RefreshTokenResultDto>.Error(403, "Akkaunt o'chirilgan.");

            await _refreshTokenStore.RevokeAsync(request.RefreshToken);

            var tokens = await GenerateAndPersistTokensAsync(user.Id, user.RoleId);

            return GenericDto<RefreshTokenResultDto>.Success(new RefreshTokenResultDto
            {
                ResultMessage = "Token muvaffaqiyatli yangilandi.",
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
                AccessTokenExpiration = tokens.AccessTokenExpiration
            });
        }

        private async Task<(string AccessToken, string RefreshToken, DateTime AccessTokenExpiration)> GenerateAndPersistTokensAsync(long userId, long? roleId)
        {
            // Token Merchant claimini to'g'ri o'rnatishi uchun to'liq entity (Merchant nav.) kerak.
            var user = (await _userRepository.GetByIdAsync(userId))!;
            var permissions = await _roleRepository.GetUserPermissionsAsync(roleId);
            var accessToken = _tokenService.GenerateAccessToken(user, permissions);
            var refreshToken = TokenPrefix + _tokenService.GenerateRefreshToken();

            await _refreshTokenStore.SaveAsync(refreshToken, userId, RefreshTokenExpiry);

            return (accessToken, refreshToken, DateTime.Now.AddMinutes(15));
        }
    }
}
