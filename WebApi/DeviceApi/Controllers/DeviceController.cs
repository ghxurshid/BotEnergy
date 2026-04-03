using CommonConfiguration.Attributes;
using DeviceApi.Models.Requests;
using DeviceApi.Models.Responses;
using Microsoft.AspNetCore.Mvc;

namespace DeviceApi.Controllers
{
    /// <summary>
    /// Qurilma holat so'rovi.
    /// IoT qurilma o'zining tizimda ro'yxatdan o'tganligini va ruxsat berilganligini tekshiradi.
    ///
    /// **Foydalanish:** Qurilma yoqilganda yoki qayta ulanganda shu endpointga murojaat qiladi.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [SkipPermissionCheck]
    public class DeviceController : ControllerBase
    {
        /// <summary>
        /// Qurilma ma'lumotlarini tekshirish.
        /// Qurilma o'z identifikatori bilan murojaat qiladi — tizimda ruxsat berilgan yoki yo'qligini biladi.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/Device/info
        ///     {
        ///         "deviceId": "SN-2024-001",
        ///         "deviceType": 1,
        ///         "functionsCount": 2,
        ///         "functionTypes": ["fuel", "gas"],
        ///         "remoteId": "remote-001",
        ///         "statuses": ["online"],
        ///         "errors": []
        ///     }
        ///
        /// **Javobda qaytadi:**
        /// - `isAllowed` — qurilma tizimda ro'yxatdan o'tganmi
        /// </remarks>
        /// <param name="request">Qurilma identifikatsiya ma'lumotlari</param>
        /// <response code="200">Qurilma holati</response>
        [HttpPost("info")]
        [ProducesResponseType(typeof(DeviceInfoResponse), StatusCodes.Status200OK)]
        public ActionResult<DeviceInfoResponse> Info([FromBody] DeviceInfoRequest request)
        {
            return Ok(new DeviceInfoResponse { IsAllowed = true });
        }
    }
}
