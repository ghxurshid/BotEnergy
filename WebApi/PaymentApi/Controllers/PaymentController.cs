using CommonConfiguration.Attributes;
using Microsoft.AspNetCore.Mvc;
using PaymentApi.Models.Requests;
using PaymentApi.Models.Responses;

namespace PaymentApi.Controllers
{
    /// <summary>
    /// To'lov operatsiyalari.
    /// Foydalanuvchi balansni to'ldirish uchun to'lov yaratadi va tasdiqlaydi.
    ///
    /// **Jarayon:**
    /// 1. **Create** → to'lov yaratiladi, QR kod generatsiya qilinadi
    /// 2. Foydalanuvchi QR kodni to'lov ilovasi orqali skanerlaydi va to'laydi
    /// 3. **Verify** → to'lov holati tekshiriladi
    ///
    /// **Hozirgi holat:** Stub implementatsiya. Kelajakda Payme/Click integratsiya qilinadi.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [SkipPermissionCheck]
    public class PaymentController : ControllerBase
    {
        /// <summary>
        /// Yangi to'lov yaratish.
        /// QR kod generatsiya qilinadi — foydalanuvchi uni to'lov ilovasida skanerlaydi.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/Payment/create
        ///     {
        ///         "userId": "12345",
        ///         "amount": 100000.00
        ///     }
        ///
        /// **amount** — so'mdagi summa
        ///
        /// **Javobda qaytadi:**
        /// - `qrCode` — generatsiya qilingan to'lov QR kodi
        /// </remarks>
        /// <param name="request">Foydalanuvchi ID va to'lov summasi</param>
        /// <response code="200">To'lov yaratildi, QR kod qaytarildi</response>
        [HttpPost("create")]
        [ProducesResponseType(typeof(CreatePaymentResponse), StatusCodes.Status200OK)]
        public ActionResult<CreatePaymentResponse> Create([FromBody] CreatePaymentRequest request)
        {
            return Ok(new CreatePaymentResponse { QrCode = "generated-payment-qr" });
        }

        /// <summary>
        /// To'lov holatini tekshirish.
        /// To'lov amalga oshirilganligini tekshiradi va balansga qo'shadi.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/Payment/verify
        ///     {
        ///         "paymentId": "pay_abc123"
        ///     }
        ///
        /// **Javobda qaytadi:**
        /// - `paid` — to'lov amalga oshirilganmi (true/false)
        /// </remarks>
        /// <param name="request">To'lov identifikatori</param>
        /// <response code="200">To'lov holati</response>
        [HttpPost("verify")]
        [ProducesResponseType(typeof(VerifyPaymentResponse), StatusCodes.Status200OK)]
        public ActionResult<VerifyPaymentResponse> Verify([FromBody] VerifyPaymentRequest request)
        {
            return Ok(new VerifyPaymentResponse { Paid = true });
        }
    }
}
