using BillingApi.Extensions;
using BillingApi.Filters;
using BillingApi.Models.Requests;
using CommonConfiguration.Attributes;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BillingApi.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class BalanceController : ControllerBase
    {
        private readonly IBillingService _billingService;

        public BalanceController(IBillingService billingService)
            => _billingService = billingService;

        /// <summary>Foydalanuvchi o'z balansini ko'radi.</summary>
        [HttpGet]
        [SkipPermissionCheck]
        public async Task<IActionResult> GetMyBalance()
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _billingService.GetBalanceAsync(userId);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>Admin: berilgan foydalanuvchi balansini to'ldiradi.</summary>
        [HttpPost]
        [TypeFilter(typeof(TopUpBalanceValidationFilter))]
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
