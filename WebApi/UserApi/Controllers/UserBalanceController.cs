using CommonConfiguration.Attributes;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace UserApi.Controllers
{
    /// <summary>
    /// Foydalanuvchi balansi (mobil ilova uchun).
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class UserBalanceController : ControllerBase
    {
        private readonly IBillingService _billingService;

        public UserBalanceController(IBillingService billingService)
            => _billingService = billingService;

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

        private bool TryGetUserId(out long userId)
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return long.TryParse(raw, out userId);
        }
    }
}
