using CommonConfiguration.Attributes;
using CommonConfiguration.Messaging;
using CommonConfiguration.Redis;
using Domain.Interfaces;
using Domain.Messaging;
using Domain.Messaging.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UserApi.Extensions;
using Permissions = Domain.Constants.Permissions;
using UserApi.Models.Requests;

namespace UserApi.Controllers
{
    /// <summary>
    /// Foydalanuvchi (Android/iOS) tomonidan chaqiriladigan sessiya endpointlari.
    ///
    /// **Sessiya jarayoni:**
    /// 1. **Create** → bo'sh sessiya yaratiladi, session_token qaytariladi (QR kod sifatida ko'rsatiladi)
    /// 2. Qurilma QR kodni skanerlaydi → MQTT → DeviceApi → RabbitMQ → UserApi orqali sessiyaga ulanadi
    /// 3. SignalR orqali `DeviceConnected` event keladi — product info bilan
    /// 4. **SetQuantity** → foydalanuvchi miqdor belgilaydi, balans tekshiriladi, qurilmaga RabbitMQ → DeviceApi → MQTT orqali start buyrug'i yuboriladi
    /// 5. Qurilma mahsulot berishni boshlaydi → SignalR orqali real-time yangilanishlar
    /// 6. **Close** → foydalanuvchi tomonidan to'xtatish
    ///
    /// **Real-time kuzatish (SignalR):**
    /// Hub URL: `ws://host:5006/hubs/session?access_token={jwt}`
    /// - `JoinSession(sessionToken)` — sessiya guruhiga qo'shilish
    /// - Server → Client eventlar: DeviceConnected, ProgressUpdate, SessionCompleted, SessionClosed
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class SessionController : ControllerBase
    {
        private readonly ISessionService _sessionService;
        private readonly RabbitMqPublisher _rabbitPublisher;
        private readonly IDeviceLockService _deviceLockService;

        public SessionController(
            ISessionService sessionService,
            RabbitMqPublisher rabbitPublisher,
            IDeviceLockService deviceLockService)
        {
            _sessionService = sessionService;
            _rabbitPublisher = rabbitPublisher;
            _deviceLockService = deviceLockService;
        }

        /// <summary>
        /// Bo'sh sessiya yaratish.
        /// Faqat foydalanuvchi ma'lumotlari bilan yaratiladi. Mahsulot qurilma ulanganida avtomatik to'ldiriladi.
        /// </summary>
        /// <response code="200">Sessiya yaratildi</response>
        /// <response code="404">Foydalanuvchi topilmadi</response>
        [HttpPost]
        [RequirePermission(Permissions.SessionCreate)]
        [ProducesResponseType(StatusCodes.Status200OK)]
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
        /// Miqdor belgilash va qurilmani ishga tushirish.
        /// Qurilma ulangandan keyin chaqiriladi. Balans tekshiriladi, limit hisoblanadi,
        /// qurilmaga RabbitMQ → DeviceApi → MQTT orqali start buyrug'i yuboriladi.
        /// </summary>
        /// <param name="request">Sessiya ID va ixtiyoriy miqdor</param>
        /// <response code="200">Miqdor belgilandi, qurilmaga start yuborildi</response>
        /// <response code="400">Qurilma ulanmagan yoki balans yetarli emas</response>
        [HttpPost]
        [RequirePermission(Permissions.SessionSetQuantity)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SetQuantity([FromBody] SetQuantityRequest request)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _sessionService.SetQuantityAsync(request.ToDto(userId));
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            // Redis orqali qurilmani band qilish
            await _deviceLockService.TryLockDeviceAsync(result.Result!.DeviceSerialNumber, userId);

            // RabbitMQ orqali DeviceApi ga start buyrug'i yuborish
            _rabbitPublisher.Publish(QueueNames.CommandQueue, new DeviceCommand
            {
                CommandType = DeviceCommandTypes.Start,
                SerialNumber = result.Result.DeviceSerialNumber,
                ProductId = result.Result.ProductId,
                Amount = result.Result.LimitQuantity
            });

            return Ok(result.Result.ToResponse());
        }

        /// <summary>
        /// Foydalanuvchi tomonidan sessiyani yopish (to'xtatish).
        /// Yetkazilgan miqdor uchun balansdan pul yechiladi.
        /// </summary>
        /// <param name="request">Yopiladigan sessiya ID si</param>
        /// <response code="200">Sessiya yopildi, balansdan yechildi</response>
        /// <response code="403">Sessiya boshqa foydalanuvchiga tegishli</response>
        [HttpPost]
        [RequirePermission(Permissions.SessionClose)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
