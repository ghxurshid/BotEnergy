using CommonConfiguration.Attributes;
using DeviceApi.Models.Requests;
using DeviceApi.Models.Responses;
using Microsoft.AspNetCore.Mvc;

namespace DeviceApi.Controllers
{
    /// <summary>
    /// Qurilma orqali to'lov tekshiruvi.
    /// Foydalanuvchi QR kod orqali to'lov qilganida qurilma bu endpointdan tasdiqlash oladi.
    ///
    /// **Hozirgi holat:** Stub implementatsiya — har doim `paymentApproved: true` qaytaradi.
    /// Kelajakda PaymentApi bilan integratsiya qilinadi.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [SkipPermissionCheck]
    public class DevicePaymentController : ControllerBase
    {
        /// <summary>
        /// To'lovni tasdiqlash.
        /// Qurilma foydalanuvchidan QR kod skanerlaydi va to'lov holatini tekshiradi.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/DevicePayment/pay
        ///     {
        ///         "deviceId": "SN-2024-001",
        ///         "userAppId": "user-app-uuid",
        ///         "qrCode": "payment-qr-12345",
        ///         "amount": 50000.00
        ///     }
        ///
        /// **Javobda qaytadi:**
        /// - `paymentApproved` — to'lov tasdiqlandi yoki yo'q
        /// </remarks>
        /// <param name="request">To'lov ma'lumotlari</param>
        /// <response code="200">To'lov holati</response>
        [HttpPost("pay")]
        [ProducesResponseType(typeof(DevicePayResponse), StatusCodes.Status200OK)]
        public ActionResult<DevicePayResponse> Pay([FromBody] DevicePayRequest request)
        {
            return Ok(new DevicePayResponse { PaymentApproved = true });
        }
    }
}
