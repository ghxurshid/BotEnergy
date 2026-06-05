using AuthApi.Extensions;
using AuthApi.Filters.ValidationFilters;
using AuthApi.Models.Requests;
using CommonConfiguration.Attributes;
using Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AuthApi.Controllers
{
    /// <summary>
    /// Platform (Manage/Merchant) autentifikatsiyasi.
    /// Self-register yo'q — foydalanuvchilarni Manage yaratadi va parol o'rnatadi.
    /// Faqat login va token yangilash mavjud.
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [SkipPermissionCheck]
    public class PlatformAuthController : ControllerBase
    {
        private readonly IPlatformAuthService _authService;

        public PlatformAuthController(IPlatformAuthService authService)
            => _authService = authService;

        /// <summary>Platform foydalanuvchi login (telefon + parol).</summary>
        [HttpPost]
        [TypeFilter(typeof(LoginValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _authService.LoginAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result!.ToResponse());
        }

        /// <summary>Platform access tokenni refresh token orqali yangilash.</summary>
        [HttpPost]
        [TypeFilter(typeof(RefreshTokenValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            var result = await _authService.RefreshTokenAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result!.ToResponse());
        }
    }
}
