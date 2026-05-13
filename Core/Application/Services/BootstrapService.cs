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
            // DbContext bitta vaqtda faqat bitta operatsiyani qo'llaydi — bu ikki query
            // bir xil scoped AppDbContext'ni ulashadi, shuning uchun ketma-ket bajaramiz.
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
