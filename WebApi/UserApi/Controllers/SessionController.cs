using CommonConfiguration.Attributes;
using Domain.Dtos.Session;
using Domain.Interfaces;
using Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserApi.Extensions;
using UserApi.Models.Requests;
using UserApi.Models.Responses;

namespace UserApi.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class SessionController : ControllerBase
    {
        private readonly ISessionService _sessionService;
        private readonly IUserRepository _userRepository;

        public SessionController(ISessionService sessionService, IUserRepository userRepository)
        {
            _sessionService = sessionService;
            _userRepository = userRepository;
        }

        [HttpPost]
        [SkipPermissionCheck]
        public async Task<IActionResult> Create()
        {
            var phoneNumber = User.Identity?.Name;
            if (string.IsNullOrEmpty(phoneNumber))
                return Unauthorized();

            var user = await _userRepository.GetByPhoneNumberAsync(phoneNumber);
            if (user is null)
                return Unauthorized();

            var result = await _sessionService.CreateSessionAsync(new CreateSessionDto
            {
                UserId = user.Id
            });

            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(new CreateSessionResponse
            {
                SessionId = result.Result!.SessionId,
                SessionToken = result.Result.SessionToken,
                ExpiresAt = result.Result.ExpiresAt,
                ResultMessage = result.Result.ResultMessage
            });
        }

        [HttpPost]
        [SkipPermissionCheck]
        public async Task<IActionResult> Close([FromBody] CloseSessionRequest request)
        {
            var phoneNumber = User.Identity?.Name;
            if (string.IsNullOrEmpty(phoneNumber))
                return Unauthorized();

            var user = await _userRepository.GetByPhoneNumberAsync(phoneNumber);
            if (user is null)
                return Unauthorized();

            var result = await _sessionService.CloseSessionByUserAsync(request.ToDto(user.Id));

            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(new CloseSessionResponse
            {
                ResultMessage = result.Result!.ResultMessage,
                TotalDelivered = result.Result.TotalDelivered
            });
        }
    }
}
