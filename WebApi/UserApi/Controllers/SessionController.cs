using CommonConfiguration.Attributes;
using Domain.Dtos.Session;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UserApi.Extensions;
using UserApi.Models.Requests;
using Permissions = Domain.Constants.Permissions;

namespace UserApi.Controllers
{
    /// <summary>
    /// Foydalanuvchi (Android/iOS) tomonidan chaqiriladigan sessiya endpointlari.
    ///
    /// **Sessiya jarayoni:**
    /// 1. <c>POST /api/Session/Create</c> — bo'sh sessiya yaratiladi, session_token (QR) qaytariladi
    /// 2. Qurilma QR ni o'qib MQTT orqali sessiyaga ulanadi → SignalR `DeviceConnected` event
    /// 3. <c>POST /api/Process/Start</c> — foydalanuvchi mahsulot tanlaydi, qurilmaga start yuboriladi
    /// 4. SignalR orqali real-time `ProcessUpdated` eventlar
    /// 5. Sessiya yopilishi: <c>POST /api/Session/Close</c> — barcha aktiv jarayonlar to'xtatiladi
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class SessionController : ControllerBase
    {
        private readonly ISessionService _sessionService;

        public SessionController(ISessionService sessionService)
        {
            _sessionService = sessionService;
        }

        /// <summary>
        /// Yangi bo'sh sessiya yaratish.
        /// UserId JWT dan olinadi.
        /// </summary>
        /// <response code="200">Sessiya yaratildi (sessionToken QR sifatida ishlatiladi)</response>
        /// <response code="403">Foydalanuvchi bloklangan</response>
        /// <response code="404">Foydalanuvchi topilmadi</response>
        [HttpPost]
        [HttpPost("/sessions")]
        [RequirePermission(Permissions.SessionCreate)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Create([FromBody] CreateSessionRequest request)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _sessionService.CreateSessionAsync(request.ToDto(userId));
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result!.ToResponse());
        }

        /// <summary>
        /// Sessiyani yopish. Aktiv jarayonlar bo'lsa, ular avval to'xtatiladi va balansdan yechiladi.
        /// </summary>
        /// <response code="200">Sessiya yopildi</response>
        /// <response code="400">Sessiya allaqachon yopilgan</response>
        /// <response code="403">Sessiya boshqa foydalanuvchiga tegishli</response>
        /// <response code="404">Sessiya topilmadi</response>
        [HttpPost]
        [RequirePermission(Permissions.SessionClose)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Close([FromBody] CloseSessionRequest request)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _sessionService.CloseSessionByUserAsync(request.ToDto(userId));
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result!.ToResponse());
        }

        [HttpPost("/sessions/{sessionId:long}/close")]
        [RequirePermission(Permissions.SessionClose)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CloseById(long sessionId)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _sessionService.CloseSessionByUserAsync(new CloseSessionDto
            {
                SessionId = sessionId,
                UserId = userId
            });
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result!.ToResponse());
        }

        private bool TryGetUserId(out long userId)
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return long.TryParse(raw, out userId);
        }
    }
}
