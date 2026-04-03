using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UsageSessionApi.Extensions;
using UsageSessionApi.Models.Requests;
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
        private readonly MqttSessionBridge _mqttBridge;

        public SessionController(ISessionService sessionService, MqttSessionBridge mqttBridge)
        {
            _sessionService = sessionService;
            _mqttBridge = mqttBridge;
        }

        /// <summary>
        /// Yangi sessiya yaratish.
        /// Foydalanuvchi QR kodni skanerlab, mahsulot tanlaydi — sessiya yaratiladi.
        /// </summary>
        [HttpPost]
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
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _sessionService.CloseSessionByUserAsync(request.ToDto(userId));
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
