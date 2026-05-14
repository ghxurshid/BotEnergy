using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Interfaces;

namespace Application.Services
{
    public sealed class BootstrapService : IBootstrapService
    {
        private readonly IUserService _userService;
        private readonly ISessionService _sessionService;

        public BootstrapService(IUserService userService, ISessionService sessionService)
        {
            _userService = userService;
            _sessionService = sessionService;
        }

        public async Task<GenericDto<BootstrapResultDto>> GetAsync(long userId)
        {
            var userTask = _userService.GetCurrentUserAsync(userId);
            var sessionTask = _sessionService.GetCurrentAsync(userId);

            await Task.WhenAll(userTask, sessionTask);

            var userResult = await userTask;
            if (!userResult.IsSuccess)
                return GenericDto<BootstrapResultDto>.Error(userResult.ErrorObj!.Code, userResult.ErrorObj.ErrorMessage);

            var sessionResult = await sessionTask;
            // Aktiv sessiya yo'qligi xato emas — null qaytaramiz.
            var activeSession = sessionResult.IsSuccess ? sessionResult.Result : null;

            return GenericDto<BootstrapResultDto>.Success(new BootstrapResultDto
            {
                User = userResult.Result!,
                ActiveSession = activeSession,
                ServerTime = DateTime.Now
            });
        }
    }
}
