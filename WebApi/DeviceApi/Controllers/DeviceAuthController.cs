using CommonConfiguration.Attributes;
using Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace DeviceApi.Controllers
{
    /// <summary>
    /// IoT qurilma autentifikatsiyasi.
    /// Qurilma birinchi marta ulanayotganda serialNumber orqali autentifikatsiya qiladi
    /// va MQTT ulanish uchun zarur bo'lgan device_token (secretKey) ni oladi.
    ///
    /// **Jarayon:**
    /// 1. Qurilma o'rnatilganda admin tomonidan ro'yxatdan o'tkaziladi (AdminApi/DeviceAdmin/Register)
    /// 2. Qurilma birinchi ishga tushganda /Authenticate endpointiga murojaat qiladi
    /// 3. Javobda device_token qaytariladi — bu token MQTT payloadlarda ishlatiladi
    /// 4. Barcha MQTT xabarlar (telemetry, session/connected, session/completed) da device_token talab qilinadi
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [SkipPermissionCheck]
    public class DeviceAuthController : ControllerBase
    {
        private readonly IDeviceRepository _deviceRepository;

        public DeviceAuthController(IDeviceRepository deviceRepository)
        {
            _deviceRepository = deviceRepository;
        }

        /// <summary>
        /// Qurilmani autentifikatsiya qilish.
        /// SerialNumber bo'yicha qurilmani topadi va device_token qaytaradi.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/DeviceAuth/Authenticate
        ///     {
        ///         "serialNumber": "SN-2024-001"
        ///     }
        ///
        /// **Javobda qaytadi:**
        /// - `deviceToken` — MQTT payloadlarda ishlatish uchun token
        /// - `serialNumber` — qurilma seriya raqami
        /// - `mqttTopicPrefix` — MQTT topic prefiksi (masalan: station/SN-2024-001)
        ///
        /// **Xatoliklar:**
        /// - 404: Qurilma topilmadi yoki faol emas
        /// </remarks>
        /// <param name="request">Qurilma seriya raqami</param>
        /// <response code="200">Autentifikatsiya muvaffaqiyatli</response>
        /// <response code="404">Qurilma topilmadi</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Authenticate([FromBody] DeviceAuthRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SerialNumber))
                return BadRequest(new { message = "SerialNumber talab qilinadi." });

            var device = await _deviceRepository.GetBySerialNumberAsync(request.SerialNumber);
            if (device is null)
                return NotFound(new { message = "Qurilma topilmadi yoki faol emas." });

            return Ok(new
            {
                deviceToken = device.SecretKey,
                serialNumber = device.SerialNumber,
                mqttTopicPrefix = $"station/{device.SerialNumber}"
            });
        }
    }

    public class DeviceAuthRequest
    {
        public required string SerialNumber { get; set; }
    }
}
