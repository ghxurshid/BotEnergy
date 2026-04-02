using Domain.Dtos.Session;
using Domain.Interfaces;
using Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UsageSessionApi.Mqtt;

namespace UsageSessionApi.Controllers
{
    /// <summary>
    /// Foydalanuvchi (Android/iOS) tomonidan chaqiriladigan sessiya endpointlari.
    /// Sessiya yaratish, yopish va qurilmaga start buyruq berish.
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class SessionController : ControllerBase
    {
        private readonly ISessionService _sessionService;
        private readonly IUserRepository _userRepository;
        private readonly MqttSessionBridge _mqttBridge;

        public SessionController(
            ISessionService sessionService,
            IUserRepository userRepository,
            MqttSessionBridge mqttBridge)
        {
            _sessionService = sessionService;
            _userRepository = userRepository;
            _mqttBridge = mqttBridge;
        }

        /// <summary>
        /// Yangi sessiya yaratish.
        /// Foydalanuvchi QR kodni skanerlab, mahsulot tanlaydi — sessiya yaratiladi.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSessionRequest request)
        {
            var phoneNumber = User.Identity?.Name;
            if (string.IsNullOrEmpty(phoneNumber))
                return Unauthorized();

            var user = await _userRepository.GetByPhoneNumberAsync(phoneNumber);
            if (user is null)
                return Unauthorized();

            var result = await _sessionService.CreateSessionAsync(new CreateSessionDto
            {
                UserId = user.Id,
                ProductId = request.ProductId,
                RequestedQuantity = request.RequestedQuantity
            });

            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            var data = result.Result!;
            return Ok(new
            {
                session_id = data.SessionId,
                session_token = data.SessionToken,
                limit_quantity = data.LimitQuantity,
                product_name = data.ProductName,
                unit = data.Unit,
                price_per_unit = data.PricePerUnit,
                expires_at = data.ExpiresAt,
                message = data.ResultMessage
            });
        }

        /// <summary>
        /// Qurilmaga start buyrug'ini yuborish.
        /// Klient sessiya yaratganidan keyin qurilmaga MQTT orqali start beradi.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Start([FromBody] StartSessionRequest request)
        {
            await _mqttBridge.PublishStartCommandAsync(
                request.DeviceSerialNumber,
                request.ProductId,
                request.Amount);

            return Ok(new { message = "Start buyrug'i qurilmaga yuborildi." });
        }

        /// <summary>
        /// Foydalanuvchi tomonidan sessiyani yopish (to'xtatish).
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Close([FromBody] CloseSessionRequest request)
        {
            var phoneNumber = User.Identity?.Name;
            if (string.IsNullOrEmpty(phoneNumber))
                return Unauthorized();

            var user = await _userRepository.GetByPhoneNumberAsync(phoneNumber);
            if (user is null)
                return Unauthorized();

            var result = await _sessionService.CloseSessionByUserAsync(new CloseSessionDto
            {
                SessionId = request.SessionId,
                UserId = user.Id
            });

            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(new
            {
                message = result.Result!.ResultMessage,
                total_delivered = result.Result.TotalDelivered
            });
        }
    }

    // ── Request modellari ──────────────────────────────────────────────

    public class CreateSessionRequest
    {
        public long ProductId { get; set; }
        public decimal? RequestedQuantity { get; set; }
    }

    public class StartSessionRequest
    {
        public string DeviceSerialNumber { get; set; } = string.Empty;
        public long ProductId { get; set; }
        public decimal Amount { get; set; }
    }

    public class CloseSessionRequest
    {
        public long SessionId { get; set; }
    }
}
