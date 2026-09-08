using CommonConfiguration.Extensions;
using AdminApi.Extensions;
using CommonConfiguration.Attributes;
using Domain.Dtos.PaymentSession;
using Domain.Enums;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Permissions = Domain.Constants.Permissions;

namespace AdminApi.Controllers
{
    /// <summary>
    /// Operator hold invoice boshqaruvi. Bu endpointlar Payme'ni CHAQIRMAYDI —
    /// maqsad holat qo'yadi, SessionApi watcher bajaradi. Har amal audit'ga (OperatorAction) yoziladi.
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class PaymentIntentAdminController : ControllerBase
    {
        private readonly IPaymentIntentAdminService _service;

        public PaymentIntentAdminController(IPaymentIntentAdminService service)
            => _service = service;

        /// <summary>To'lovlar ro'yxati (filter: merchant/session/status/usul/sana).</summary>
        [HttpGet]
        [RequirePermission(Permissions.HoldAdminGetAll)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> All(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] long? merchantId = null,
            [FromQuery] long? sessionId = null,
            [FromQuery] PaymentIntentStatus? status = null,
            [FromQuery] PaymentMethod? method = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            var skip = (Math.Max(pageNumber, 1) - 1) * pageSize;
            var result = await _service.ListAsync(
                skip, pageSize, merchantId, sessionId, status, method, from, to, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }

        /// <summary>Bitta to'lov tafsilotlari.</summary>
        [HttpGet("{intentId:long}")]
        [RequirePermission(Permissions.HoldAdminGetById)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ById(long intentId)
        {
            var result = await _service.GetByIdAsync(intentId, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }

        /// <summary>To'lov audit step'lari (to'liq jsonb izi).</summary>
        [HttpGet("{intentId:long}")]
        [RequirePermission(Permissions.HoldAdminGetSteps)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Steps(long intentId)
        {
            var result = await _service.GetStepsAsync(intentId, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }

        /// <summary>[EXPERT] Majburiy capture (ishlatilgan summa yoki berilgan amount).</summary>
        [HttpPost("{intentId:long}")]
        [RequirePermission(Permissions.HoldAdminCapture)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Capture(long intentId, [FromBody] PaymentIntentOperatorActionDto request)
        {
            if (!TryGetUserId(out var adminUserId)) return Unauthorized();
            var result = await _service.ForceCaptureAsync(intentId, request, adminUserId, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }

        /// <summary>[EXPERT] Majburiy refund (hold'ni qaytarish).</summary>
        [HttpPost("{intentId:long}")]
        [RequirePermission(Permissions.HoldAdminRefund)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Refund(long intentId, [FromBody] PaymentIntentOperatorActionDto request)
        {
            if (!TryGetUserId(out var adminUserId)) return Unauthorized();
            var result = await _service.ForceRefundAsync(intentId, request, adminUserId, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }

        /// <summary>[EXPERT] To'lovgacha bekor qilish (Created/WaitingForConfirmation).</summary>
        [HttpPost("{intentId:long}")]
        [RequirePermission(Permissions.HoldAdminCancel)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Cancel(long intentId, [FromBody] PaymentIntentOperatorActionDto request)
        {
            if (!TryGetUserId(out var adminUserId)) return Unauthorized();
            var result = await _service.ForceCancelAsync(intentId, request, adminUserId, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }

        /// <summary>[EXPERT] Failed invoice'ni qayta navbatga qo'yish.</summary>
        [HttpPost("{intentId:long}")]
        [RequirePermission(Permissions.HoldAdminRetry)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Retry(long intentId, [FromBody] PaymentIntentOperatorActionDto request)
        {
            if (!TryGetUserId(out var adminUserId)) return Unauthorized();
            var result = await _service.RetryAsync(intentId, request, adminUserId, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }

        private bool TryGetUserId(out long userId)
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return long.TryParse(raw, out userId);
        }
    }
}
