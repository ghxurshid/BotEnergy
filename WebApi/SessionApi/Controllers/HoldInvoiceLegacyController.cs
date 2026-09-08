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
    /// ESKI sirt — <c>/api/HoldInvoice/*</c>. To'lov strategiya abstraksiyasidan keyin
    /// haqiqiy sirt <see cref="SessionPaymentController"/> (<c>/api/SessionPayment/*</c>);
    /// bu controller dalada ishlayotgan mobil ilova sinmasligi uchun BIR RELIZ saqlanadi
    /// va faqat yangi servisga yo'naltiradi.
    ///
    /// Javob shakli yangisi bilan bir xil (<c>intentId</c>, <c>fundedTiyin</c>) — eski
    /// nomlar faqat real-time event'da (SessionBalanceChangedDto) dublikat qilinadi.
    /// </summary>
    [Route("api/HoldInvoice/[action]")]
    [ApiController]
    [Authorize]
    [ApiExplorerSettings(GroupName = "legacy")]
    [Obsolete("SessionPaymentController ishlating — keyingi relizda olib tashlanadi.")]
    public class HoldInvoiceLegacyController : ControllerBase
    {
        private readonly ISessionPaymentService _payments;

        public HoldInvoiceLegacyController(ISessionPaymentService payments)
            => _payments = payments;

        /// <summary>Eski nom: hold invoice yaratish → <c>SessionPayment/CreateIntent</c>.</summary>
        [HttpPost]
        [Idempotent(Required = true)]
        [RequirePermission(Permissions.PaymentHoldCreate)]
        [ProducesResponseType(typeof(PaymentIntentResultDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Create([FromBody] CreatePaymentIntentRequest request, CancellationToken ct)
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

        /// <summary>Eski nom: invoice bekor qilish → <c>SessionPayment/CancelIntent</c>.</summary>
        [HttpPost("{invoiceId}")]
        [RequirePermission(Permissions.PaymentHoldCancel)]
        [ProducesResponseType(typeof(PaymentIntentResultDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Cancel(long invoiceId, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _payments.CancelIntentAsync(invoiceId, userId, ct);
            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }

        /// <summary>Eski nom: sessiya invoice'lari → <c>SessionPayment/BySession</c>.</summary>
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

        /// <summary>Eski nom: sessiya balansi → <c>SessionPayment/Balance</c>.</summary>
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
}
