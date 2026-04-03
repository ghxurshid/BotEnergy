using BillingApi.Extensions;
using BillingApi.Filters.PermissionFilters;
using BillingApi.Filters.ValidationFilters;
using BillingApi.Models.Requests;
using CommonConfiguration.Attributes;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BillingApi.Controllers
{
    /// <summary>
    /// Balans boshqaruvi.
    /// Foydalanuvchi o'z balansini ko'radi, admin esa foydalanuvchi balansini to'ldiradi.
    ///
    /// **Imkoniyatlar:**
    /// - GetMyBalance — joriy foydalanuvchi balansini ko'rish (JWT talab qilinadi)
    /// - TopUp — berilgan foydalanuvchi balansini to'ldirish (admin permission talab qilinadi)
    ///
    /// **Balans turlari:**
    /// - NaturalUser (jismoniy shaxs) → to'g'ridan-to'g'ri `user.balance`
    /// - LegalUser (yuridik shaxs) → `organization.balance` (tashkilot balansi)
    ///
    /// **Cheklovlar:**
    /// - JWT token talab qilinadi
    /// - TopUp faqat tegishli permissionga ega admin bajarishi mumkin
    /// - Balans manfiy bo'la olmaydi
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class BalanceController : ControllerBase
    {
        private readonly IBillingService _billingService;

        public BalanceController(IBillingService billingService)
            => _billingService = billingService;

        /// <summary>
        /// Joriy foydalanuvchi balansini ko'rish.
        /// JWT tokendagi user_id bo'yicha balans qaytariladi.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     GET /api/Balance/GetMyBalance
        ///     Headers: Authorization: Bearer eyJhbGciOiJI...
        ///
        /// **Javobda qaytadi:**
        /// - `balance` — joriy balans (so'mda)
        /// - `currency` — valyuta
        /// </remarks>
        /// <response code="200">Balans ma'lumotlari</response>
        /// <response code="401">Token yo'q yoki yaroqsiz</response>
        [HttpGet]
        [SkipPermissionCheck]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyBalance()
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _billingService.GetBalanceAsync(userId);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Foydalanuvchi balansini to'ldirish (admin).
        /// Berilgan foydalanuvchining balansiga summa qo'shiladi.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/Balance/TopUp
        ///     Headers: Authorization: Bearer eyJhbGciOiJI...
        ///     {
        ///         "userId": 5,
        ///         "amount": 500000.00
        ///     }
        ///
        /// **amount** — qo'shiladigan summa (so'mda). Musbat bo'lishi shart.
        ///
        /// **Xatoliklar:**
        /// - 404: Foydalanuvchi topilmadi
        /// - 400: Summa noto'g'ri (0 yoki manfiy)
        /// </remarks>
        /// <param name="request">Foydalanuvchi ID va summa</param>
        /// <response code="200">Balans to'ldirildi</response>
        /// <response code="404">Foydalanuvchi topilmadi</response>
        [HttpPost]
        [RequirePermission(Permissions.BalanceTopUp)]
        [TypeFilter(typeof(TopUpBalanceValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> TopUp([FromBody] TopUpBalanceRequest request)
        {
            var result = await _billingService.TopUpAsync(request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        private bool TryGetUserId(out long userId)
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return long.TryParse(raw, out userId);
        }
    }
}
