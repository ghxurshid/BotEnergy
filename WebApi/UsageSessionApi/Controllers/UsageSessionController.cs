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
    /// Mahsulot berish sessiyasini yaratish, qurilmaga start buyruq berish va sessiyani yopish.
    ///
    /// **Sessiya jarayoni:**
    /// 1. Foydalanuvchi QR kodni skanerlaydi → UserApi/DeviceConnection/GetProducts → mahsulotlarni ko'radi
    /// 2. **Create** → sessiya yaratiladi, session_token va limit qaytariladi
    /// 3. **Start** → qurilmaga MQTT orqali "start" buyrug'i yuboriladi
    /// 4. Qurilma mahsulot berishni boshlaydi → SignalR orqali real-time yangilanishlar keladi
    /// 5. **Close** → foydalanuvchi tomonidan to'xtatish (yoki qurilma o'zi tugallaydi)
    ///
    /// **Real-time kuzatish (SignalR):**
    /// Hub URL: `ws://host:5003/hubs/session?access_token={jwt}`
    /// - `JoinSession(sessionToken)` — sessiya guruhiga qo'shilish
    /// - Server → Client eventlar: DeviceConnected, ProgressUpdate, SessionCompleted, SessionClosed
    ///
    /// **Cheklovlar:**
    /// - JWT token talab qilinadi
    /// - Sessiya yaratilganda balans tekshiriladi — yetarli bo'lmasa limit mos ravishda kamaytiriladi
    /// - Sessiya 30 daqiqa harakatsiz qolsa avtomatik yopiladi (TimedOut)
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
        /// Yangi sessiya yaratish.
        /// Foydalanuvchi QR kodni skanerlab, mahsulot tanlaydi — sessiya yaratiladi.
        /// Balans asosida limit hisoblanadi.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/Session/Create
        ///     Headers: Authorization: Bearer eyJhbGciOiJI...
        ///     {
        ///         "productId": 5,
        ///         "requestedQuantity": 20.0
        ///     }
        ///
        /// **requestedQuantity** ixtiyoriy — berilmasa balansga mos maksimal miqdor hisoblanadi.
        ///
        /// **Javobda qaytadi:**
        /// - `session_id` — sessiya ID
        /// - `session_token` — sessiya tokeni (SignalR va qurilma aloqasi uchun)
        /// - `limit_quantity` — ruxsat berilgan maksimal miqdor
        /// - `product_name` — mahsulot nomi
        /// - `unit` — o'lchov birligi (Litr, KWh, Kubometr)
        /// - `price_per_unit` — birlik narxi
        /// - `expires_at` — sessiya amal qilish muddati
        ///
        /// **Xatoliklar:**
        /// - 404: Mahsulot topilmadi
        /// - 404: Foydalanuvchi topilmadi
        /// </remarks>
        /// <param name="request">Mahsulot ID va ixtiyoriy miqdor</param>
        /// <response code="200">Sessiya yaratildi — token va limit qaytarildi</response>
        /// <response code="404">Mahsulot yoki foydalanuvchi topilmadi</response>
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
        /// Qurilmaga start buyrug'ini yuborish (MQTT orqali).
        /// Sessiya yaratilganidan keyin qurilmaga "ishni boshlash" buyrug'i beriladi.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/Session/Start
        ///     Headers: Authorization: Bearer eyJhbGciOiJI...
        ///     {
        ///         "deviceSerialNumber": "SN-2024-001",
        ///         "productId": 5,
        ///         "amount": 20.0
        ///     }
        ///
        /// **MQTT topic:** `station/{serialNumber}/command/start`
        ///
        /// **Eslatma:** Bu endpoint faqat qurilmaga buyruq yuboradi.
        /// Qurilma ulanganda SignalR orqali `DeviceConnected` event keladi.
        /// </remarks>
        /// <param name="request">Qurilma serial raqami, mahsulot va miqdor</param>
        /// <response code="200">Start buyrug'i qurilmaga yuborildi</response>
        [HttpPost]
        [RequirePermission(Permissions.SessionStart)]
        [ProducesResponseType(StatusCodes.Status200OK)]
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
        /// Yetkazilgan miqdor uchun balansdan pul yechiladi.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/Session/Close
        ///     Headers: Authorization: Bearer eyJhbGciOiJI...
        ///     {
        ///         "sessionId": 42
        ///     }
        ///
        /// **Javobda qaytadi:**
        /// - `message` — natija xabari
        /// - `total_delivered` — jami yetkazilgan miqdor
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
