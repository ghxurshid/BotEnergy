using Application.Helpers;
using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Enums;
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
                existingUser.IsOtpVerified = false;
                await _userRepository.UpdateUserAsync(existingUser);
                await _otpService.GenerateOtpAsync(existingUser.PhoneNumber, OtpPurpose.Register);

                return GenericDto<RegisterResultDto>.Success(
                    new RegisterResultDto { ResultMessage = "OTP kod qayta yuborildi." });
            }

            var newUser = new UserEntity
            {
                PhoneId = request.PhoneId,
                PhoneNumber = request.PhoneNumber,
                Mail = request.Mail,
                IsVerified = false,
                IsOtpVerified = false
            };

            await _userRepository.CreateUserAsync(newUser);
            await _otpService.GenerateOtpAsync(newUser.PhoneNumber, OtpPurpose.Register);

            return GenericDto<RegisterResultDto>.Success(
                new RegisterResultDto { ResultMessage = "Ro'yxatdan o'tdingiz. OTP kod yuborildi." });
        }

        public async Task<GenericDto<VerifyResultDto>> VerifyAsync(VerifyDto request)
        {
            var user = await _userRepository.GetByPhoneNumberAsync(request.PhoneNumber);
            if (user is null)
                return GenericDto<VerifyResultDto>.Error(404, "Bu telefon raqam ro'yxatdan o'tmagan.");

            var isOtpValid = await _otpService.VerifyOtpAsync(user.PhoneNumber, request.OtpCode, OtpPurpose.Register);
            if (!isOtpValid)
                return GenericDto<VerifyResultDto>.Error(400, "OTP kod noto'g'ri yoki muddati o'tgan.");

            user.IsOtpVerified = true;
            await _userRepository.UpdateUserAsync(user);

            return GenericDto<VerifyResultDto>.Success(
                new VerifyResultDto { ResultMessage = "OTP tasdiqlandi. Parol o'rnatish uchun /SetPassword ga murojaat qiling." });
        }

        public async Task<GenericDto<SetPasswordResultDto>> SetPasswordAsync(SetPasswordDto request)
        {
            var user = await _userRepository.GetByPhoneNumberAsync(request.PhoneNumber);
            if (user is null)
                return GenericDto<SetPasswordResultDto>.Error(404, "Bu telefon raqam ro'yxatdan o'tmagan.");

            if (!user.IsOtpVerified)
                return GenericDto<SetPasswordResultDto>.Error(403, "OTP tasdiqlanmagan. Avval /Verify ga murojaat qiling.");

            if (user.IsVerified)
                return GenericDto<SetPasswordResultDto>.Error(400, "Foydalanuvchi allaqachon ro'yxatdan o'tgan.");

            var (hash, salt) = PasswordHelper.CreatePassword(request.Password);

            user.PasswordHash = hash;
            user.PasswordSalt = salt;
            user.IsVerified = true;
            user.LastLoginDate = DateTime.Now;
            await _userRepository.UpdateUserAsync(user);

            var accessToken = _tokenService.GenerateAccessToken(user);
            var refreshToken = _tokenService.GenerateRefreshToken();

            return GenericDto<SetPasswordResultDto>.Success(new SetPasswordResultDto
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

            if (user.PasswordHash is null)
            {
                if (!user.IsOtpVerified)
                    return GenericDto<LoginResultDto>.Error(403, "Ro'yxatdan o'tish tugallanmagan. 2-bosqich: OTP tasdiqlash (/Verify).");
                return GenericDto<LoginResultDto>.Error(403, "Ro'yxatdan o'tish tugallanmagan. 3-bosqich: Parol o'rnatish (/SetPassword).");
            }

            var isPasswordValid = PasswordHelper.Verify(request.Password, user.PasswordHash, user.PasswordSalt!);
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

        public async Task<GenericDto<ResetPasswordRequestResultDto>> ResetPasswordRequestAsync(ResetPasswordRequestDto request)
        {
            var user = await _userRepository.GetByPhoneNumberAsync(request.PhoneNumber);
            if (user is null)
                return GenericDto<ResetPasswordRequestResultDto>.Error(404, "Bu telefon raqam ro'yxatdan o'tmagan.");

            if (!user.IsVerified)
                return GenericDto<ResetPasswordRequestResultDto>.Error(403, "Faqat to'liq ro'yxatdan o'tgan foydalanuvchilar parolni tiklashi mumkin.");

            await _otpService.GenerateOtpAsync(user.PhoneNumber, OtpPurpose.ResetPassword);

            return GenericDto<ResetPasswordRequestResultDto>.Success(
                new ResetPasswordRequestResultDto { ResultMessage = "Parolni tiklash uchun OTP kod yuborildi." });
        }

        public async Task<GenericDto<ResetPasswordVerifyResultDto>> ResetPasswordVerifyAsync(ResetPasswordVerifyDto request)
        {
            var user = await _userRepository.GetByPhoneNumberAsync(request.PhoneNumber);
            if (user is null)
                return GenericDto<ResetPasswordVerifyResultDto>.Error(404, "Bu telefon raqam ro'yxatdan o'tmagan.");

            if (!user.IsVerified)
                return GenericDto<ResetPasswordVerifyResultDto>.Error(403, "Faqat to'liq ro'yxatdan o'tgan foydalanuvchilar parolni tiklashi mumkin.");

            var isOtpValid = await _otpService.VerifyOtpAsync(user.PhoneNumber, request.OtpCode, OtpPurpose.ResetPassword);
            if (!isOtpValid)
                return GenericDto<ResetPasswordVerifyResultDto>.Error(400, "OTP kod noto'g'ri yoki muddati o'tgan.");

            return GenericDto<ResetPasswordVerifyResultDto>.Success(
                new ResetPasswordVerifyResultDto { ResultMessage = "OTP tasdiqlandi. Yangi parol o'rnatish uchun /ResetPasswordSet ga murojaat qiling." });
        }

        public async Task<GenericDto<ResetPasswordSetResultDto>> ResetPasswordSetAsync(ResetPasswordSetDto request)
        {
            var user = await _userRepository.GetByPhoneNumberAsync(request.PhoneNumber);
            if (user is null)
                return GenericDto<ResetPasswordSetResultDto>.Error(404, "Bu telefon raqam ro'yxatdan o'tmagan.");

            if (!user.IsVerified)
                return GenericDto<ResetPasswordSetResultDto>.Error(403, "Faqat to'liq ro'yxatdan o'tgan foydalanuvchilar parolni tiklashi mumkin.");

            var isOtpVerified = await _otpService.IsOtpVerifiedAsync(user.PhoneNumber, OtpPurpose.ResetPassword);
            if (!isOtpVerified)
                return GenericDto<ResetPasswordSetResultDto>.Error(403, "OTP tasdiqlanmagan. Avval /ResetPasswordVerify ga murojaat qiling.");

            var (hash, salt) = PasswordHelper.CreatePassword(request.NewPassword);
            user.PasswordHash = hash;
            user.PasswordSalt = salt;
            await _userRepository.UpdateUserAsync(user);

            await _otpService.ConsumeOtpVerificationAsync(user.PhoneNumber, OtpPurpose.ResetPassword);

            return GenericDto<ResetPasswordSetResultDto>.Success(
                new ResetPasswordSetResultDto { ResultMessage = "Parol muvaffaqiyatli o'zgartirildi." });
        }
    }
}
