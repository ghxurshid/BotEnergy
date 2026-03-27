using AuthApi.Models.Requests;
using Domain.Dtos;

namespace UserApi.Extensions
{
    public static class RequestToDtoExtensions
    {
        public static RegisterDto ToDto(this RegisterRequest request)
        {
            return new RegisterDto
            {
                PhoneId = request.PhoneId,
                Mail = request.Mail,
                PhoneNumber = request.PhoneNumber
            };
        }

        public static VerifyDto ToDto(this VerifyRequest request)
        {
            return new VerifyDto
            {
                PhoneNumber = request.PhoneNumber,
                OtpCode = request.OtpCode
            };
        }

        public static SetPasswordDto ToDto(this SetPasswordRequest request)
        {
            return new SetPasswordDto
            {
                PhoneNumber = request.PhoneNumber,
                Password = request.Password
            };
        }

        public static LoginDto ToDto(this LoginRequest request)
        {
            return new LoginDto
            {
                PhoneNumber = request.PhoneNumber,
                Password = request.Password
            };
        }

        public static RefreshTokenDto ToDto(this RefreshTokenRequest request)
        {
            return new RefreshTokenDto
            {
                RefreshToken = request.RefreshToken
            };
        }

        public static ResetPasswordRequestDto ToDto(this ResetPasswordRequestRequest request)
        {
            return new ResetPasswordRequestDto
            {
                PhoneNumber = request.PhoneNumber
            };
        }

        public static ResetPasswordVerifyDto ToDto(this ResetPasswordVerifyRequest request)
        {
            return new ResetPasswordVerifyDto
            {
                PhoneNumber = request.PhoneNumber,
                OtpCode = request.OtpCode
            };
        }

        public static ResetPasswordSetDto ToDto(this ResetPasswordSetRequest request)
        {
            return new ResetPasswordSetDto
            {
                PhoneNumber = request.PhoneNumber,
                NewPassword = request.NewPassword
            };
        }
    }
}
