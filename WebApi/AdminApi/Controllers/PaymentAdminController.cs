using AdminApi.Models.Requests;
using CommonConfiguration.Attributes;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Permissions = Domain.Constants.Permissions;

namespace AdminApi.Controllers
{
    /// <summary>
    /// Admin uchun to'lov audit endpointlari.
    /// Tranzaksiyalar ro'yxati, har birining audit step'lari, va manual reverse.
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class PaymentAdminController : ControllerBase
    {
        private readonly IPaymentTransactionRepository _repo;
        private readonly IPaymentService _paymentService;

        public PaymentAdminController(
            IPaymentTransactionRepository repo,
            IPaymentService paymentService)
        {
            _repo = repo;
            _paymentService = paymentService;
        }

        /// <summary>
        /// Barcha to'lov tranzaksiyalari — status va sana oralig'i bo'yicha filtr.
        /// </summary>
        [HttpGet]
        [RequirePermission(Permissions.PaymentAdminGetAll)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> All(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] PaymentStatus? status = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            var skip = (Math.Max(pageNumber, 1) - 1) * pageSize;
            var items = await _repo.ListAllAsync(skip, pageSize, status, from, to);
            return Ok(items);
        }

        /// <summary>Bitta tranzaksiya tafsilotlari (step'lar bilan).</summary>
        [HttpGet("{transactionId:long}")]
        [RequirePermission(Permissions.PaymentAdminGetById)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ById(long transactionId)
        {
            var tx = await _repo.GetByIdAsync(transactionId, includeSteps: true);
            if (tx is null)
                return NotFound(new { message = "To'lov topilmadi." });

            return Ok(tx);
        }

        /// <summary>Tranzaksiyaning to'liq audit step'lari (vaqt bo'yicha tartibda).</summary>
        [HttpGet("{transactionId:long}")]
        [RequirePermission(Permissions.PaymentAdminGetSteps)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Steps(long transactionId)
        {
            var steps = await _repo.GetStepsAsync(transactionId);
            return Ok(steps);
        }

        /// <summary>
        /// Muvaffaqiyatli to'lovni qo'lda bekor qilish (balansdan ayirish + Status=Reversed).
        /// Audit step'iga `Reversed` qadami yoziladi.
        /// </summary>
        /// <response code="200">Reverse muvaffaqiyatli</response>
        /// <response code="400">Reason bo'sh yoki tx Succeeded holatda emas</response>
        /// <response code="404">Tranzaksiya yoki user topilmadi</response>
        /// <response code="500">Balans egasi turi mos kelmadi</response>
        [HttpPost("{transactionId:long}")]
        [RequirePermission(Permissions.PaymentAdminReverse)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Reverse(long transactionId, [FromBody] ReversePaymentRequest request)
        {
            if (!TryGetUserId(out var adminUserId))
                return Unauthorized();

            var result = await _paymentService.ReverseAsync(transactionId, adminUserId, request.Reason);
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result);
        }

        private bool TryGetUserId(out long userId)
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return long.TryParse(raw, out userId);
        }
    }
}
