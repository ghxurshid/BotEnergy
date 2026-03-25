using AuthApi.Filters;
using AuthApi.Models.Requests;
using Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using UserApi.Extensions;

namespace UserApi.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost]
        [TypeFilter(typeof(RegisterValidationFilter))]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var result = await _authService.RegisterAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result);
        }

        [HttpPost]
        [TypeFilter(typeof(VerifyValidationFilter))]
        public async Task<IActionResult> Verify([FromBody] VerifyRequest request)
        {
            var result = await _authService.VerifyAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result);
        }

        [HttpPost]
        [TypeFilter(typeof(LoginValidationFilter))]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _authService.LoginAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result);
        }

        [HttpPost]
        [TypeFilter(typeof(RefreshTokenValidationFilter))]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            var result = await _authService.RefreshTokenAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result);
        }
    }
}