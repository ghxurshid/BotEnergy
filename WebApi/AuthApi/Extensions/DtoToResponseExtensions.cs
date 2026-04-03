using AuthApi.Models.Responses;
using Domain.Dtos;

namespace AuthApi.Extensions
{
    public static class DtoToResponseExtensions
    {
        public static RegisterResponse ToResponse(this RegisterResultDto dto)
            => new RegisterResponse { Message = dto.ResultMessage };

        public static VerifyResponse ToResponse(this VerifyResultDto dto)
            => new VerifyResponse { ResultMessage = dto.ResultMessage };

        public static SetPasswordResponse ToResponse(this SetPasswordResultDto dto)
            => new SetPasswordResponse
            {
                AccessToken = dto.AccessToken,
                RefreshToken = dto.RefreshToken,
                AccessTokenExpiration = dto.AccessTokenExpiration
            };

        public static LoginResponse ToResponse(this LoginResultDto dto)
            => new LoginResponse
            {
                AccessToken = dto.AccessToken,
                RefreshToken = dto.RefreshToken,
                AccessTokenExpiration = dto.AccessTokenExpiration
            };

        public static RefreshTokenResponse ToResponse(this RefreshTokenResultDto dto)
            => new RefreshTokenResponse
            {
                AccessToken = dto.AccessToken,
                RefreshToken = dto.RefreshToken,
                AccessTokenExpiration = dto.AccessTokenExpiration,
                ResultMessage = dto.ResultMessage
            };

        public static ResetPasswordRequestResponse ToResponse(this ResetPasswordRequestResultDto dto)
            => new ResetPasswordRequestResponse { ResultMessage = dto.ResultMessage };

        public static ResetPasswordVerifyResponse ToResponse(this ResetPasswordVerifyResultDto dto)
            => new ResetPasswordVerifyResponse { ResultMessage = dto.ResultMessage };

        public static ResetPasswordSetResponse ToResponse(this ResetPasswordSetResultDto dto)
            => new ResetPasswordSetResponse { ResultMessage = dto.ResultMessage };
    }
}
