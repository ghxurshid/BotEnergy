using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Interfaces;

namespace Application.Services
{
    public class AuthService : IAuthService
    {
        public Task<GenericDto<LoginResultDto>> LoginAsync(LoginDto request) =>
            Task.FromException<GenericDto<LoginResultDto>>(new NotImplementedException());

        public Task<GenericDto<RefreshTokenResultDto>> RefreshTokenAsync(RefreshTokenDto request) =>
            Task.FromException<GenericDto<RefreshTokenResultDto>>(new NotImplementedException());

        public Task<GenericDto<RegisterResultDto>> RegisterAsync(RegisterDto request) =>
            Task.FromException<GenericDto<RegisterResultDto>>(new NotImplementedException());

        public Task<GenericDto<VerifyResultDto>> VerifyAsync(VerifyDto request) =>
            Task.FromException<GenericDto<VerifyResultDto>>(new NotImplementedException());
    }
}
