using CommonConfiguration.Attributes;
using CommonConfiguration.Extensions;
using CommonConfiguration.Filters;
using Domain.Dtos.PaymentSession;
using Domain.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Permissions = Domain.Constants.Permissions;

namespace SessionApi.Controllers
{
    /// <summary>
    /// Sessiya uchun to'lov — usuldan QAT'I NAZAR bir xil sirt. Qaysi strategiya ishlashini
    /// (Merchant / Invoice / Subscribe) merchant o'z sozlamasida tanlaydi; mijoz uchun
    /// endpointlar o'zgarmaydi.
    ///
    /// **Oqim:**
    /// 1. Sessiya ochiq holda mobil `CreateIntent` bilan summa ajratishni so'raydi
    /// 2. Server sessiyada qotirilgan usul bo'yicha provider bilan ishlaydi:
    ///    Subscribe → hold (pul bloklanadi), Invoice/Merchant → pul darhol yechiladi
    /// 3. Javobdagi `requiresUserAction`/`checkoutUrl` mijoz nima qilishini aytadi
    /// 4. Balans real-time SignalR (`SessionBalanceChanged`) + MQTT (`balance.update`) orqali keladi
    /// 5. Dispense mablag'ni FIFO tartibda yeydi; sessiya yopilishida capture yoki refund
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class SessionPaymentController : ControllerBase
    {
        private readonly ISessionPaymentService _payments;

        public SessionPaymentController(ISessionPaymentService payments)
            => _payments = payments;

        /// <summary>
        /// Sessiya uchun yangi to'lov (intent) yaratish.
        /// Idempotency-Key header MAJBURIY (retry'da takror to'lov ochilmasligi uchun).
        /// </summary>
        /// <response code="200">To'lov yaratildi — javobdagi status/checkoutUrl bo'yicha davom eting</response>
        /// <response code="400">Summa noto'g'ri yoki aktiv to'lovlar limiti oshdi</response>
        /// <response code="403">Sessiya boshqa foydalanuvchiniki</response>
        /// <response code="404">Sessiya topilmadi</response>
        /// <response code="409">Sessiya holati ruxsat bermaydi yoki merchant to'lov usuli sozlanmagan</response>
        /// <response code="502">Provider bilan bog'lanishda xatolik</response>
        [HttpPost]
        [Idempotent(Required = true)]
        [RequirePermission(Permissions.PaymentHoldCreate)]
        [ProducesResponseType(typeof(PaymentIntentResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> CreateIntent([FromBody] CreatePaymentIntentRequest request, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var idempotencyKey = Request.Headers.TryGetValue(IdempotencyFilter.HeaderName, out var keyHeader)
                ? keyHeader.FirstOrDefault()
                : null;

            var result = await _payments.CreateIntentAsync(new CreatePaymentIntentDto
            {
                SessionId = request.SessionId,
                UserId = userId,
                AmountUzs = request.AmountUzs,
                Phone = request.Phone,
                CardId = request.CardId,
                IdempotencyKey = idempotencyKey
            }, ct);

            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }

        /// <summary>
        /// Umuman ishlatilmagan to'lovni bekor qilish (mablag' olingan bo'lsa qaytariladi).
        /// </summary>
        /// <response code="200">Bekor qilindi / qaytarish navbatga qo'yildi</response>
        /// <response code="403">To'lov boshqa foydalanuvchiniki</response>
        /// <response code="404">To'lov topilmadi</response>
        /// <response code="409">To'lov qisman ishlatilgan yoki holati ruxsat bermaydi</response>
        [HttpPost("{intentId}")]
        [RequirePermission(Permissions.PaymentHoldCancel)]
        [ProducesResponseType(typeof(PaymentIntentResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CancelIntent(long intentId, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _payments.CancelIntentAsync(intentId, userId, ct);
            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }

        /// <summary>Sessiyaning barcha to'lovlari (FIFO tartibda).</summary>
        [HttpGet("{sessionId}")]
        [RequirePermission(Permissions.PaymentHoldRead)]
        [ProducesResponseType(typeof(List<PaymentIntentItemDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> BySession(long sessionId)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _payments.GetIntentsForSessionAsync(sessionId, userId);
            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }

        /// <summary>Sessiya to'lov konteksti: usul + balans + to'lovlar ro'yxati.</summary>
        [HttpGet("{sessionId}")]
        [RequirePermission(Permissions.PaymentHoldRead)]
        [ProducesResponseType(typeof(PaymentSessionDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Balance(long sessionId)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _payments.GetForSessionAsync(sessionId, userId);
            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }

        private bool TryGetUserId(out long userId)
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return long.TryParse(raw, out userId);
        }
    }

    /// <summary>Sessiya uchun to'lov yaratish so'rovi.</summary>
    public class CreatePaymentIntentRequest
    {
        public long SessionId { get; set; }

        /// <summary>Ajratiladigan summa, so'mda.</summary>
        public decimal AmountUzs { get; set; }

        /// <summary>Invoice usulida chek yuboriladigan telefon (ixtiyoriy).</summary>
        public string? Phone { get; set; }

        /// <summary>Subscribe usulida qaysi saqlangan karta bilan to'lansin (ixtiyoriy — default karta).</summary>
        public long? CardId { get; set; }
    }
}
