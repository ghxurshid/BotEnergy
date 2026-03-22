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
            var existing = _userRepository.GetByPhoneNumber(request.PhoneNumber);

            if (existing.Result != null)
                return GenericDto<RegisterResultDto>.Error(-5, "User already exists");

            var (hash, salt) = PasswordHelper.CreatePassword(request.Password);

            var user = new UserEntity
            {                
                PhoneNumber = request.PhoneNumber,
                Mail = request.Mail,
                PhoneId = request.PhoneId,
                PasswordHash = hash,
                PasswordSalt = salt,
                IsVerified = false
            };

            _userRepository.CreateUser(user);

            await _otpService.GenerateOtpAsync(user.PhoneNumber);

            return GenericDto<RegisterResultDto>.Success(new RegisterResultDto
            {
                ResultMessage = "OTP sent"
            });
        }

        public async Task<GenericDto<VerifyResultDto>> VerifyAsync(VerifyDto request)
        {
            var user = _userRepository.GetByPhoneNumber(request.PhoneNumber).Result;

            if (user == null)
                return GenericDto<VerifyResultDto>.Error(-404, "User not found");

            var isValid = await _otpService.VerifyOtpAsync(request.PhoneNumber, request.OtpCode);

            if (!isValid)
                return GenericDto<VerifyResultDto>.Error(-5, "Invalid OTP");

            user.IsVerified = true;
            _userRepository.UpdateUser(user);

            var token = _tokenService.GenerateAccessToken(user);
            var refresh = _tokenService.GenerateRefreshToken();

            return GenericDto<VerifyResultDto>.Success(new VerifyResultDto
            {
                AccessToken = token,
                RefreshToken = refresh,
                AccessTokenExpiration = DateTime.Now.AddMinutes(5)
            });
        }

        public async Task<GenericDto<LoginResultDto>> LoginAsync(LoginDto request)
        {
            var user = _userRepository.GetByPhoneNumber(request.PhoneNumber).Result;

            if (user == null)
                return GenericDto<LoginResultDto>.Error(-5, "User not found");

            if (!PasswordHelper.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
                return GenericDto<LoginResultDto>.Error(-5, "Wrong password");

            if (!user.IsVerified)
                return GenericDto<LoginResultDto>.Error(-5, "User not verified");

            var token = _tokenService.GenerateAccessToken(user);
            var refresh = _tokenService.GenerateRefreshToken();

            return GenericDto<LoginResultDto>.Success(new LoginResultDto
            {
                AccessToken = token,
                RefreshToken = refresh,
                AccessTokenExpiration = DateTime.Now.AddMinutes(5)
            });
        }

        public async Task<GenericDto<RefreshTokenResultDto>> RefreshTokenAsync(RefreshTokenDto request)
        {
            // Oddiy variant (prod’da DBda saqlaysan)
            //var newToken = _tokenService.GenerateAccessToken(new UserEntity
            //{
            //    Id = request.UserId,
            //    PhoneNumber = request.PhoneNumber
            //});

            //return GenericDto<RefreshTokenResultDto>.Success(new RefreshTokenResultDto
            //{
            //    AccessToken = newToken
            //});
            return default;
        }
    }
}
