using CommonConfiguration.Attributes;
using CommonConfiguration.Filters;
using Domain.Dtos.Payment;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UserApi.Models.Requests;
using Permissions = Domain.Constants.Permissions;

namespace UserApi.Controllers
{
    /// <summary>
    /// Mobile app uchun Payme orqali QR balans to'ldirish endpointlari.
    ///
    /// **Oqim:**
    /// 1. User Payme app'da "To'lash" → QR generate
    /// 2. Mobile QR'dan token oladi va summa bilan birga shu endpointga yuboradi
    /// 3. Server Payme: receipts.create → receipts.pay → state==4 ⇒ balans to'ldiriladi
    /// 4. Audit izi PaymentTransactionStep'larda saqlanadi
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IPaymentTransactionRepository _repo;
        private readonly IUserRepository _userRepo;
        private readonly ISessionNotifier _notifier;

        public PaymentController(
            IPaymentService paymentService,
            IPaymentTransactionRepository repo,
            IUserRepository userRepo,
            ISessionNotifier notifier)
        {
            _paymentService = paymentService;
            _repo = repo;
            _userRepo = userRepo;
            _notifier = notifier;
        }

        /// <summary>
        /// QR orqali balansni to'ldirish.
        /// Idempotency-Key header tavsiya etiladi (mobile retry'lariga qarshi).
        /// </summary>
        /// <response code="200">To'lov muvaffaqiyatli, balans yangilandi</response>
        /// <response code="400">Noto'g'ri so'rov (summa, token yoki PayeeType)</response>
        /// <response code="402">Payme to'lovni rad etdi (state != 4 yoki token noto'g'ri)</response>
        /// <response code="403">Foydalanuvchi bloklangan yoki permission yetarli emas</response>
        /// <response code="404">Foydalanuvchi topilmadi</response>
        /// <response code="500">Ichki xatolik (Payme to'lashdi, balans yangilanmadi — manual reconciliation)</response>
        /// <response code="502">Payme bilan bog'lanishda xatolik</response>
        [HttpPost]
        [Idempotent]
        // Permission tekshiruvi controller ichida — PayeeType bo'yicha har xil permission talab qilinadi.
        // Shuning uchun PermissionFilter chetlab o'tilib, qo'lda HasPermission chaqiriladi.
        [SkipPermissionCheck]
        [ProducesResponseType(typeof(QrTopUpResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> QrTopUp([FromBody] QrTopUpRequest request, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            // PayeeType bo'yicha permission tekshirish: User → TopUpSelf, Organization → TopUpOrganization
            var requiredPermission = request.PayeeType == PaymentPayeeType.Organization
                ? Permissions.PaymentTopUpOrganization
                : Permissions.PaymentTopUpSelf;

            if (!HasPermission(requiredPermission))
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = $"'{requiredPermission}' ruxsati yo'q." });

            var idempotencyKey = Request.Headers.TryGetValue(IdempotencyFilter.HeaderName, out var keyHeader)
                ? keyHeader.FirstOrDefault()
                : null;

            var dto = new QrTopUpRequestDto
            {
                InitiatedByUserId = userId,
                PayeeType = request.PayeeType,
                Amount = request.Amount,
                PaymeToken = request.PaymeToken,
                SessionId = request.SessionId,
                IdempotencyKey = idempotencyKey
            };

            var result = await _paymentService.ProcessQrTopUpAsync(dto, ct);
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            // Foydalanuvchining barcha qurilmalariga real-time xabar — boshqa device'da ham balans yangilanadi
            if (result.Result!.Status == PaymentStatus.Succeeded)
            {
                await _notifier.NotifyUserAsync(userId, "PaymentSucceeded", new
                {
                    transactionId = result.Result.TransactionId,
                    payeeType = request.PayeeType.ToString(),
                    amount = request.Amount,
                    newBalance = result.Result.NewBalance
                });
            }

            return Ok(result.Result);
        }

        /// <summary>Foydalanuvchining shaxsiy to'lov tarixi.</summary>
        [HttpGet]
        [RequirePermission(Permissions.PaymentGetMyTransactions)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> MyTransactions(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] PaymentStatus? status = null)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var skip = (Math.Max(pageNumber, 1) - 1) * pageSize;
            var items = await _repo.ListForUserAsync(userId, skip, pageSize, status);
            return Ok(items);
        }

        /// <summary>
        /// Org owner uchun: o'z tashkilotining to'lov tarixi.
        /// Tashkilot id'si caller'ning LegalUser profilidan olinadi (URL parametri qabul qilinmaydi —
        /// boshqa orgning ma'lumotlarini ko'rishni oldini olish uchun).
        /// </summary>
        [HttpGet]
        [RequirePermission(Permissions.PaymentGetOrganizationTransactions)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> OrganizationTransactions(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] PaymentStatus? status = null)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var user = await _userRepo.GetByIdAsync(userId);
            if (user is not LegalUserEntity legal || legal.OrganizationId is null)
                return BadRequest(new { message = "Yuridik foydalanuvchi va biriktirilgan tashkilot kerak." });

            var skip = (Math.Max(pageNumber, 1) - 1) * pageSize;
            var items = await _repo.ListForOrganizationAsync(legal.OrganizationId.Value, skip, pageSize, status);
            return Ok(items);
        }

        private bool TryGetUserId(out long userId)
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return long.TryParse(raw, out userId);
        }

        private bool HasPermission(string permission)
            => User.Claims.Any(c => c.Type == "Permission" && c.Value == permission);
    }
}
