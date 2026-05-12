using CommonConfiguration.Attributes;
using Domain.Dtos.Base;
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
    /// 1. <c>POST /api/Session/Create</c> — pending sessiya yaratiladi (cache, 30 min TTL), DB'ga yozilmaydi.
    ///    Response: <c>{ userId, sessionToken }</c> → QR kod sifatida ko'rsatiladi.
    /// 2. Qurilma reader QR ni o'qib MQTT orqali DeviceApi'ga yuboradi.
    /// 3. DeviceApi gRPC orqali UserApi'dan tokenni so'raydi, solishtiradi, mos kelsa DB'da sessiyani
    ///    Connected statusda yaratadi va RabbitMQ orqali "connected" event yuboradi.
    /// 4. UserApi event'ni qabul qilib SignalR <c>DeviceConnected</c> event'ini mobile'ga yuboradi.
    /// 5. <c>POST /api/Process/Start</c> — foydalanuvchi mahsulot tanlaydi, qurilmaga start yuboriladi.
    /// 6. SignalR orqali real-time <c>ProcessUpdated</c> eventlar.
    /// 7. Sessiya yopilishi: <c>POST /api/Session/Close</c> — barcha aktiv jarayonlar to'xtatiladi.
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
        /// Pending sessiya yaratish (cache'da, 30 min TTL). DB'ga yozilmaydi — sessiya
        /// qurilma ulanganda DeviceApi tomonidan yaratiladi. UserId JWT dan olinadi.
        /// Body talab qilinmaydi. Pending mavjud bo'lsa idempotent qaytariladi.
        /// </summary>
        /// <response code="200">Pending sessiya yaratildi yoki mavjud token qaytarildi (QR uchun userId+sessionToken)</response>
        /// <response code="403">Foydalanuvchi bloklangan</response>
        /// <response code="404">Foydalanuvchi topilmadi</response>
        /// <response code="409">Foydalanuvchida allaqachon DB'da faol sessiya bor</response>
        [HttpPost]
        [HttpPost("/sessions")]
        [RequirePermission(Permissions.SessionCreate)]
        [Idempotent]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Create()
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _sessionService.CreateSessionAsync(new CreateSessionDto { UserId = userId });
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

        /// <summary>
        /// Foydalanuvchining hozirgi aktiv sessiyasini olish (resume uchun).
        /// Aktiv sessiya yo'q bo'lsa <c>activeSession: null</c> qaytadi.
        /// </summary>
        [HttpGet]
        [HttpGet("/sessions/current")]
        [RequirePermission(Permissions.SessionRead)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Current()
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _sessionService.GetCurrentAsync(userId);
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(new { activeSession = result.Result });
        }

        /// <summary>
        /// Sessiya tafsilotlari (id bo'yicha). Faqat o'z sessiyangizni ko'ra olasiz.
        /// </summary>
        [HttpGet("{sessionId:long}")]
        [HttpGet("/sessions/{sessionId:long}")]
        [RequirePermission(Permissions.SessionRead)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long sessionId)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _sessionService.GetByIdAsync(sessionId, userId);
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result);
        }

        /// <summary>
        /// Sessiya tarixi (paginated). Filtrlash uchun ?from=&amp;to= ishlatish mumkin.
        /// </summary>
        [HttpGet]
        [HttpGet("/sessions/history")]
        [RequirePermission(Permissions.SessionRead)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> History([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var pagination = new PaginationParams { PageNumber = pageNumber, PageSize = pageSize };
            var result = await _sessionService.GetHistoryAsync(userId, pagination, from, to);
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result);
        }

        /// <summary>
        /// App foreground'da sliding idle timeout uchun ping. 30-60 soniyada bir marta yuboriladi.
        /// </summary>
        [HttpPost("{sessionId:long}")]
        [HttpPost("/sessions/{sessionId:long}/heartbeat")]
        [RequirePermission(Permissions.SessionHeartbeat)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Heartbeat(long sessionId)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _sessionService.HeartbeatAsync(sessionId, userId);
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result);
        }

        private bool TryGetUserId(out long userId)
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return long.TryParse(raw, out userId);
        }
    }
}
