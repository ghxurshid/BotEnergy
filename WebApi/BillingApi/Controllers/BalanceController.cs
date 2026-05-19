using BillingApi.Extensions;
using Permissions = Domain.Constants.Permissions;
using BillingApi.Filters.ValidationFilters;
using BillingApi.Models.Requests;
using CommonConfiguration.Attributes;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BillingApi.Controllers
{
    /// <summary>
    /// Balans boshqaruvi (admin).
    /// Admin foydalanuvchi balansini to'ldiradi.
    /// Foydalanuvchi o'z balansini ko'rish uchun UserApi `/api/UserBalance/GetMyBalance` ishlatadi.
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
    }
}
