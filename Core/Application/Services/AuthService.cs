using Application.Helpers;
using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IOtpService _otpService;
        private readonly ITokenService _tokenService;

        public AuthService(
            IUserRepository userRepository,
            IOtpService otpService,
            ITokenService tokenService)
        {
            _userRepository = userRepository;
            _otpService = otpService;
            _tokenService = tokenService;
        }

        public async Task<GenericDto<RegisterResultDto>> RegisterAsync(RegisterDto request)
        {
            var existingUser = await _userRepository.GetByPhoneNumberAsync(request.PhoneNumber);

            if (existingUser is not null)
            {
                existingUser.IsVerified = false;
                await _userRepository.UpdateUserAsync(existingUser);
                await _otpService.GenerateOtpAsync(existingUser.PhoneNumber);

                return GenericDto<RegisterResultDto>.Success(
                    new RegisterResultDto { ResultMessage = "OTP kod qayta yuborildi." });
            }

            var (hash, salt) = PasswordHelper.CreatePassword(request.Password);

            var newUser = new UserEntity
            {
                PhoneId = request.PhoneId,
                PhoneNumber = request.PhoneNumber,
                Mail = request.Mail,
                PasswordHash = hash,
                PasswordSalt = salt,
                IsVerified = false
            };

            await _userRepository.CreateUserAsync(newUser);
            await _otpService.GenerateOtpAsync(newUser.PhoneNumber);

            return GenericDto<RegisterResultDto>.Success(
                new RegisterResultDto { ResultMessage = "Ro'yxatdan o'tdingiz. OTP kod yuborildi." });
        }

        public async Task<GenericDto<VerifyResultDto>> VerifyAsync(VerifyDto request)
        {
            var user = await _userRepository.GetByPhoneNumberAsync(request.PhoneNumber);
            if (user is null)
                return GenericDto<VerifyResultDto>.Error(404, "Bu telefon raqam ro'yxatdan o'tmagan.");

            var isOtpValid = await _otpService.VerifyOtpAsync(user.PhoneNumber, request.OtpCode);
            if (!isOtpValid)
                return GenericDto<VerifyResultDto>.Error(400, "OTP kod noto'g'ri yoki muddati o'tgan.");

            user.IsVerified = true;
            user.LastLoginDate = DateTime.Now;
            await _userRepository.UpdateUserAsync(user);

            var accessToken = _tokenService.GenerateAccessToken(user);
            var refreshToken = _tokenService.GenerateRefreshToken();

            return GenericDto<VerifyResultDto>.Success(new VerifyResultDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpiration = DateTime.Now.AddMinutes(15)
            });
        }

        public async Task<GenericDto<LoginResultDto>> LoginAsync(LoginDto request)
        {
            var user = await _userRepository.GetByPhoneNumberAsync(request.PhoneNumber);
            if (user is null)
                return GenericDto<LoginResultDto>.Error(404, "Telefon raqam yoki parol noto'g'ri.");

            var isPasswordValid = PasswordHelper.Verify(request.Password, user.PasswordHash, user.PasswordSalt);
            if (!isPasswordValid)
                return GenericDto<LoginResultDto>.Error(401, "Telefon raqam yoki parol noto'g'ri.");

            if (!user.IsVerified)
                return GenericDto<LoginResultDto>.Error(403, "Akkaunt tasdiqlanmagan. Iltimos OTP orqali tasdiqlang.");

            if (user.IsBlocked)
                return GenericDto<LoginResultDto>.Error(403, "Akkaunt bloklangan.");

            user.LastLoginDate = DateTime.Now;
            user.LastActiveDate = DateTime.Now;
            await _userRepository.UpdateUserAsync(user);

            var accessToken = _tokenService.GenerateAccessToken(user);
            var refreshToken = _tokenService.GenerateRefreshToken();

            return GenericDto<LoginResultDto>.Success(new LoginResultDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpiration = DateTime.Now.AddMinutes(15)
            });
        }

        public async Task<GenericDto<RefreshTokenResultDto>> RefreshTokenAsync(RefreshTokenDto request)
        {
            return GenericDto<RefreshTokenResultDto>.Error(501, "Refresh token hali implement qilinmagan.");
        }
    }
}