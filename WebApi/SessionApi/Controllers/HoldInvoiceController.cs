using CommonConfiguration.Attributes;
using CommonConfiguration.Filters;
using Domain.Dtos.PaymentSession;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Permissions = Domain.Constants.Permissions;

namespace SessionApi.Controllers
{
    /// <summary>
    /// Hold invoice (Payme pre-authorization) — mobil oqim.
    ///
    /// **Oqim:**
    /// 1. Sessiya ochiq holda mobil `Create` bilan summani bloklashni so'raydi
    /// 2. Server device egasi merchant credential'lari bilan Payme hold receipt yaratadi
    /// 3. Mijoz Payme ilovasida to'laydi — watcher polling bilan Hold holatini aniqlaydi
    /// 4. Balans real-time SignalR (`SessionBalanceChanged`) + MQTT (`balance.update`) orqali keladi
    /// 5. Dispense hold balansidan FIFO tartibda yeydi; sessiya yopilishida capture/refund
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class HoldInvoiceController : ControllerBase
    {
        private readonly IHoldInvoiceService _invoiceService;
        private readonly IPaymentSessionService _paymentSessionService;

        public HoldInvoiceController(
            IHoldInvoiceService invoiceService,
            IPaymentSessionService paymentSessionService)
        {
            _invoiceService = invoiceService;
            _paymentSessionService = paymentSessionService;
        }

        /// <summary>
        /// Yangi hold invoice yaratish — summa Payme'da bloklanadi.
        /// Idempotency-Key header MAJBURIY (retry'da takror invoice ochilmasligi uchun).
        /// </summary>
        /// <response code="200">Invoice yaratildi — mijoz Payme ilovasida tasdiqlashi kerak</response>
        /// <response code="400">Summa noto'g'ri yoki invoice limiti (10) oshdi</response>
        /// <response code="403">Sessiya boshqa foydalanuvchiniki</response>
        /// <response code="404">Sessiya topilmadi</response>
        /// <response code="409">Sessiya holati ruxsat bermaydi (Paused/Settling) yoki merchant Payme sozlanmagan</response>
        /// <response code="502">Payme bilan bog'lanishda xatolik</response>
        [HttpPost]
        [Idempotent(Required = true)]
        [RequirePermission(Permissions.PaymentHoldCreate)]
        [ProducesResponseType(typeof(HoldInvoiceResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> Create([FromBody] CreateHoldInvoiceRequest request, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var idempotencyKey = Request.Headers.TryGetValue(IdempotencyFilter.HeaderName, out var keyHeader)
                ? keyHeader.FirstOrDefault()
                : null;

            var result = await _invoiceService.CreateAsync(new CreateHoldInvoiceDto
            {
                SessionId = request.SessionId,
                UserId = userId,
                AmountUzs = request.AmountUzs,
                Phone = request.Phone,
                IdempotencyKey = idempotencyKey
            }, ct);

            return result.IsSuccess
                ? Ok(result.Result)
                : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Umuman ishlatilmagan invoice'ni bekor qilish (to'langan bo'lsa pul qaytariladi).
        /// </summary>
        /// <response code="200">Bekor qilindi / refund navbatga qo'yildi</response>
        /// <response code="403">Invoice boshqa foydalanuvchiniki</response>
        /// <response code="404">Invoice topilmadi</response>
        /// <response code="409">Invoice qisman ishlatilgan yoki holati ruxsat bermaydi</response>
        [HttpPost("{invoiceId}")]
        [RequirePermission(Permissions.PaymentHoldCancel)]
        [ProducesResponseType(typeof(HoldInvoiceResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Cancel(long invoiceId, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _invoiceService.CancelByUserAsync(invoiceId, userId, ct);
            return result.IsSuccess
                ? Ok(result.Result)
                : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>Sessiyaning barcha hold invoice'lari (FIFO tartibda).</summary>
        [HttpGet("{sessionId}")]
        [RequirePermission(Permissions.PaymentHoldRead)]
        [ProducesResponseType(typeof(List<HoldInvoiceItemDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> BySession(long sessionId)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _invoiceService.GetForSessionAsync(sessionId, userId);
            return result.IsSuccess
                ? Ok(result.Result)
                : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>Sessiya to'lov konteksti: hold balans + invoice ro'yxati.</summary>
        [HttpGet("{sessionId}")]
        [RequirePermission(Permissions.PaymentHoldRead)]
        [ProducesResponseType(typeof(PaymentSessionDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Balance(long sessionId)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _paymentSessionService.GetForSessionAsync(sessionId, userId);
            return result.IsSuccess
                ? Ok(result.Result)
                : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        private bool TryGetUserId(out long userId)
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return long.TryParse(raw, out userId);
        }
    }

    /// <summary>Hold invoice yaratish so'rovi.</summary>
    public class CreateHoldInvoiceRequest
    {
        public long SessionId { get; set; }

        /// <summary>Bloklanadigan summa, so'mda.</summary>
        public decimal AmountUzs { get; set; }

        /// <summary>SMS invoice uchun telefon (ixtiyoriy).</summary>
        public string? Phone { get; set; }
    }
}
