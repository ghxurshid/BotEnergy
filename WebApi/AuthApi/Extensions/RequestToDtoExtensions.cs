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
                PhoneNumber = request.PhoneNumber,
                Password = request.Password
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
    }
}
