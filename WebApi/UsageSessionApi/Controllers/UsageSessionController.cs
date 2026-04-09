using CommonConfiguration.Attributes;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UsageSessionApi.Extensions;
using UsageSessionApi.Filters.PermissionFilters;
using UsageSessionApi.Models.Requests;
using UsageSessionApi.Mqtt;

namespace UsageSessionApi.Controllers
{
    /// <summary>
    /// Foydalanuvchi (Android/iOS) tomonidan chaqiriladigan sessiya endpointlari.
    ///
    /// **Yangi sessiya jarayoni:**
    /// 1. **Create** → bo'sh sessiya yaratiladi, session_token qaytariladi (QR kod sifatida ko'rsatiladi)
    /// 2. Qurilma QR kodni skanerlaydi → MQTT orqali sessiyaga ulanadi, mahsulot avtomatik to'ldiriladi
    /// 3. SignalR orqali `DeviceConnected` event keladi — product info bilan
    /// 4. **SetQuantity** → foydalanuvchi miqdor belgilaydi, balans tekshiriladi, qurilmaga start buyrug'i yuboriladi
    /// 5. Qurilma mahsulot berishni boshlaydi → SignalR orqali real-time yangilanishlar
    /// 6. **Close** → foydalanuvchi tomonidan to'xtatish (yoki qurilma o'zi tugallaydi)
    ///
    /// **Real-time kuzatish (SignalR):**
    /// Hub URL: `ws://host:5003/hubs/session?access_token={jwt}`
    /// - `JoinSession(sessionToken)` — sessiya guruhiga qo'shilish
    /// - Server → Client eventlar: DeviceConnected, ProgressUpdate, SessionCompleted, SessionClosed
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class UsageSessionController : ControllerBase
    {
        private readonly ISessionService _sessionService;
        private readonly MqttSessionBridge _mqttBridge;

        public UsageSessionController(ISessionService sessionService, MqttSessionBridge mqttBridge)
        {
            _sessionService = sessionService;
            _mqttBridge = mqttBridge;
        }

        /// <summary>
        /// Bo'sh sessiya yaratish.
        /// Faqat foydalanuvchi ma'lumotlari bilan yaratiladi. Mahsulot qurilma ulanganida avtomatik to'ldiriladi.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/UsageSession/Create
        ///     Headers: Authorization: Bearer eyJhbGciOiJI...
        ///     { }
        ///
        /// **Javobda qaytadi:**
        /// - `sessionId` — sessiya ID
        /// - `sessionToken` — QR kod sifatida qurilmaga ko'rsatiladi
        /// - `expiresAt` — sessiya amal qilish muddati (30 daqiqa)
        ///
        /// Keyingi qadam: sessionToken ni QR kod qilib qurilmaga ko'rsating.
        /// Qurilma skanerlagandan keyin SignalR orqali `DeviceConnected` event keladi.
        /// </remarks>
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
        /// qurilmaga MQTT orqali start buyrug'i yuboriladi.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/UsageSession/SetQuantity
        ///     Headers: Authorization: Bearer eyJhbGciOiJI...
        ///     {
        ///         "sessionId": 42,
        ///         "requestedQuantity": 20.0
        ///     }
        ///
        /// **requestedQuantity** ixtiyoriy — berilmasa balansga mos maksimal miqdor hisoblanadi.
        ///
        /// **Javobda qaytadi:**
        /// - `limitQuantity` — ruxsat berilgan miqdor
        /// - `productName` — mahsulot nomi
        /// - `unit` — o'lchov birligi
        /// - `pricePerUnit` — birlik narxi
        ///
        /// **Xatoliklar:**
        /// - 400: Qurilma hali ulanmagan
        /// - 400: Balans yetarli emas
        /// - 403: Sessiya sizga tegishli emas
        /// </remarks>
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

            // Qurilmaga MQTT orqali start buyrug'i yuborish
            await _mqttBridge.PublishStartCommandAsync(
                result.Result!.DeviceSerialNumber,
                result.Result.ProductId,
                result.Result.LimitQuantity);

            return Ok(result.Result.ToResponse());
        }

        /// <summary>
        /// Foydalanuvchi tomonidan sessiyani yopish (to'xtatish).
        /// Yetkazilgan miqdor uchun balansdan pul yechiladi.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/UsageSession/Close
        ///     Headers: Authorization: Bearer eyJhbGciOiJI...
        ///     {
        ///         "sessionId": 42
        ///     }
        ///
        /// **Javobda qaytadi:**
        /// - `message` — natija xabari
        /// - `totalDelivered` — jami yetkazilgan miqdor
        ///
        /// **Xatoliklar:**
        /// - 403: Sessiya sizga tegishli emas
        /// - 400: Sessiya allaqachon yopilgan
        /// - 404: Sessiya topilmadi
        /// </remarks>
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
