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
            // Ikkala chaqiruv bitta scoped AppDbContext ustida ishlaydi — Task.WhenAll
            // bilan parallel awaitlash "A second operation was started on this context"
            // xatosini keltirib chiqaradi. Shuning uchun sekvensial await.
            var userResult = await _userService.GetCurrentUserAsync(userId);
            if (!userResult.IsSuccess)
                return GenericDto<BootstrapResultDto>.Error(userResult.ErrorObj!.Code, userResult.ErrorObj.ErrorMessage);

            var sessionResult = await _sessionService.GetCurrentAsync(userId);
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
