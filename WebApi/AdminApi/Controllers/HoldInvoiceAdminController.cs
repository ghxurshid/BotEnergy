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
    public class HoldInvoiceAdminController : ControllerBase
    {
        private readonly IHoldInvoiceAdminService _service;

        public HoldInvoiceAdminController(IHoldInvoiceAdminService service)
            => _service = service;

        /// <summary>Hold invoice'lar ro'yxati (filter: merchant/session/status/sana).</summary>
        [HttpGet]
        [RequirePermission(Permissions.HoldAdminGetAll)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> All(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] long? merchantId = null,
            [FromQuery] long? sessionId = null,
            [FromQuery] HoldInvoiceStatus? status = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            var skip = (Math.Max(pageNumber, 1) - 1) * pageSize;
            var result = await _service.ListAsync(skip, pageSize, merchantId, sessionId, status, from, to, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>Bitta invoice tafsilotlari.</summary>
        [HttpGet("{invoiceId:long}")]
        [RequirePermission(Permissions.HoldAdminGetById)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ById(long invoiceId)
        {
            var result = await _service.GetByIdAsync(invoiceId, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>Invoice audit step'lari (to'liq jsonb izi).</summary>
        [HttpGet("{invoiceId:long}")]
        [RequirePermission(Permissions.HoldAdminGetSteps)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Steps(long invoiceId)
        {
            var result = await _service.GetStepsAsync(invoiceId, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>[EXPERT] Majburiy capture (ishlatilgan summa yoki berilgan amount).</summary>
        [HttpPost("{invoiceId:long}")]
        [RequirePermission(Permissions.HoldAdminCapture)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Capture(long invoiceId, [FromBody] HoldInvoiceOperatorActionDto request)
        {
            if (!TryGetUserId(out var adminUserId)) return Unauthorized();
            var result = await _service.ForceCaptureAsync(invoiceId, request, adminUserId, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>[EXPERT] Majburiy refund (hold'ni qaytarish).</summary>
        [HttpPost("{invoiceId:long}")]
        [RequirePermission(Permissions.HoldAdminRefund)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Refund(long invoiceId, [FromBody] HoldInvoiceOperatorActionDto request)
        {
            if (!TryGetUserId(out var adminUserId)) return Unauthorized();
            var result = await _service.ForceRefundAsync(invoiceId, request, adminUserId, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>[EXPERT] To'lovgacha bekor qilish (Created/WaitingForConfirmation).</summary>
        [HttpPost("{invoiceId:long}")]
        [RequirePermission(Permissions.HoldAdminCancel)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Cancel(long invoiceId, [FromBody] HoldInvoiceOperatorActionDto request)
        {
            if (!TryGetUserId(out var adminUserId)) return Unauthorized();
            var result = await _service.ForceCancelAsync(invoiceId, request, adminUserId, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>[EXPERT] Failed invoice'ni qayta navbatga qo'yish.</summary>
        [HttpPost("{invoiceId:long}")]
        [RequirePermission(Permissions.HoldAdminRetry)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Retry(long invoiceId, [FromBody] HoldInvoiceOperatorActionDto request)
        {
            if (!TryGetUserId(out var adminUserId)) return Unauthorized();
            var result = await _service.RetryAsync(invoiceId, request, adminUserId, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        private bool TryGetUserId(out long userId)
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return long.TryParse(raw, out userId);
        }
    }
}
