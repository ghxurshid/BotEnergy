using Domain.Dtos;
using Domain.Dtos.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Interfaces
{
    public interface IAuthService
    {
        Task<GenericDto<RegisterResultDto>> RegisterAsync(RegisterDto request);
        Task<GenericDto<VerifyResultDto>> VerifyAsync(VerifyDto request);
        Task<GenericDto<LoginResultDto>> LoginAsync(LoginDto request);
        Task<GenericDto<RefreshTokenResultDto>> RefreshTokenAsync(RefreshTokenDto request);
    }
}
