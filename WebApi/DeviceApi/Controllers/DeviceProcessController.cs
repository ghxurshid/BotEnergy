using CommonConfiguration.Attributes;
using DeviceApi.Models.Requests;
using DeviceApi.Models.Responses;
using Microsoft.AspNetCore.Mvc;

namespace DeviceApi.Controllers
{
    /// <summary>
    /// Qurilma jarayonini boshqarish (legacy).
    /// Qurilma mahsulot berish jarayonini boshlash/to'xtatish uchun buyruq oladi.
    ///
    /// **Hozirgi holat:** Stub implementatsiya.
    /// Asosiy sessiya boshqaruvi UsageSessionApi ga ko'chirilgan.
    /// Bu endpoint faqat legacy qurilmalar uchun saqlanib qolgan.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [SkipPermissionCheck]
    public class DeviceProcessController : ControllerBase
    {
        /// <summary>
        /// Qurilma jarayonini boshqarish (legacy stub).
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/DeviceProcess/process
        ///     {
        ///         "deviceId": "SN-2024-001",
        ///         "productId": "5",
        ///         "unitType": "litr",
        ///         "amount": 20.0,
        ///         "userAppId": "user-app-uuid",
        ///         "beginEnd": "BEGIN",
        ///         "endReason": ""
        ///     }
        ///
        /// **beginEnd qiymatlari:** "BEGIN" — boshlash, "END" — to'xtatish
        ///
        /// **Javobda qaytadi:**
        /// - `limitAmount` — ruxsat berilgan maksimal miqdor
        /// - `productId` — mahsulot ID
        /// - `command` — buyruq ("END")
        /// </remarks>
        /// <param name="request">Jarayon boshqaruv buyrug'i</param>
        /// <response code="200">Buyruq qabul qilindi</response>
        [HttpPost("process")]
        [ProducesResponseType(typeof(DeviceProcessResponse), StatusCodes.Status200OK)]
        public ActionResult<DeviceProcessResponse> Process([FromBody] DeviceProcessRequest request)
        {
            return Ok(new DeviceProcessResponse
            {
                LimitAmount = 1000,
                ProductId = request.ProductId,
                Command = "END"
            });
        }
    }
}
