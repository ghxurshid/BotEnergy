using CommonConfiguration.Attributes;
using Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using UsageSessionApi.Extensions;
using UsageSessionApi.Models.Requests;

namespace UsageSessionApi.Controllers
{
    /// <summary>
    /// IoT qurilma tomonidan chaqiriladigan HTTP fallback endpointlari.
    /// MQTT ishlamagan holatlarda qurilma to'g'ridan-to'g'ri shu endpointlarga murojaat qiladi.
    ///
    /// **Jarayon (qurilma tomoni):**
    /// 1. **Connect** — qurilma sessiyaga ulanadi (session_token + serial_number)
    /// 2. **ReportProgress** — har bir porsiya mahsulot berilganida progress yuboriladi
    /// 3. **Finish** — qurilma sessiyani tugallaydi (yakuniy miqdor + sabab)
    ///
    /// **Autentifikatsiya:** Talab qilinmaydi — qurilma `session_token` bilan identifikatsiya qilinadi.
    ///
    /// **Cheklovlar:**
    /// - session_token yaroqsiz bo'lsa 404 qaytadi
    /// - serial_number sessiyaga mos kelmasa 403 qaytadi
    /// - Sessiya aktiv bo'lmasa (allaqachon yopilgan) 400 qaytadi
    ///
    /// **Eslatma:** Asosiy aloqa MQTT orqali amalga oshiriladi.
    /// Bu endpointlar faqat MQTT ulana olmagan holatlar uchun zaxira sifatida ishlaydi.
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [SkipPermissionCheck]
    public class DeviceSessionController : ControllerBase
    {
        private readonly ISessionService _sessionService;

        public DeviceSessionController(ISessionService sessionService)
        {
            _sessionService = sessionService;
        }

        /// <summary>
        /// Qurilmani sessiyaga ulash.
        /// Qurilma session_token va o'z serial_number ini yuboradi.
        /// Sessiya statusi Pending → DeviceConnected ga o'tadi.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/DeviceSession/Connect
        ///     {
        ///         "sessionToken": "a1b2c3d4e5f6...",
        ///         "serialNumber": "SN-2024-001"
        ///     }
        ///
        /// **Xatoliklar:**
        /// - 404: Sessiya topilmadi (noto'g'ri token)
        /// - 400: Sessiya pending holatida emas
        /// - 404: Qurilma topilmadi yoki faol emas
        /// </remarks>
        /// <param name="request">Sessiya tokeni va qurilma seriya raqami</param>
        /// <response code="200">Qurilma sessiyaga ulandi</response>
        /// <response code="404">Sessiya yoki qurilma topilmadi</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Connect([FromBody] DeviceConnectRequest request)
        {
            var result = await _sessionService.DeviceConnectAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result!.ToResponse());
        }

        /// <summary>
        /// Mahsulot berish progressini yuborish.
        /// Qurilma har bir porsiya (masalan, har 0.5 litr) berilganda bu endpointga chaqiruv qiladi.
        /// Miqdor sessiyaga qo'shib boriladi (summirovka).
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/DeviceSession/ReportProgress
        ///     {
        ///         "sessionToken": "a1b2c3d4e5f6...",
        ///         "serialNumber": "SN-2024-001",
        ///         "quantity": 0.5
        ///     }
        ///
        /// **Muhim:** `quantity` — bu porsiya miqdori, jami emas.
        /// Masalan: 0.5, 0.5, 0.5 = jami 1.5 litr
        ///
        /// Har bir progressda SignalR orqali klientga `ProgressUpdate` event yuboriladi.
        ///
        /// **Xatoliklar:**
        /// - 403: Qurilma bu sessiyaga tegishli emas
        /// - 400: Sessiya aktiv holatda emas
        /// </remarks>
        /// <param name="request">Sessiya tokeni, serial raqam va porsiya miqdori</param>
        /// <response code="200">Progress qabul qilindi</response>
        /// <response code="403">Qurilma sessiyaga tegishli emas</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> ReportProgress([FromBody] DeviceProgressRequest request)
        {
            var result = await _sessionService.ReportProgressAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result!.ToResponse());
        }

        /// <summary>
        /// Sessiyani tugallash (qurilma tomoni).
        /// Qurilma mahsulot berishni tugatganda yakuniy miqdor va sabab bilan chaqiriladi.
        /// Balansdan pul yechiladi.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/DeviceSession/Finish
        ///     {
        ///         "sessionToken": "a1b2c3d4e5f6...",
        ///         "serialNumber": "SN-2024-001",
        ///         "finalQuantity": 15.75,
        ///         "endReason": "completed"
        ///     }
        ///
        /// **endReason qiymatlari:**
        /// - `completed` — normal tugadi
        /// - `limit_reached` — limit tugadi
        /// - `device_error` — qurilma xatosi
        /// - `user_stopped` — foydalanuvchi to'xtatdi
        ///
        /// **Javobda qaytadi:**
        /// - `total_delivered` — jami berilgan miqdor
        /// - `message` — natija xabari
        /// </remarks>
        /// <param name="request">Yakuniy miqdor va tugallanish sababi</param>
        /// <response code="200">Sessiya tugallandi, balansdan yechildi</response>
        /// <response code="403">Qurilma sessiyaga tegishli emas</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Finish([FromBody] DeviceFinishRequest request)
        {
            var result = await _sessionService.DeviceFinishAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result!.ToResponse());
        }
    }
}
