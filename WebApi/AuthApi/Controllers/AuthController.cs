using AuthApi.Models.Requests;
using Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using UserApi.Extensions;

namespace UserApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var result = await _authService.RegisterAsync(request.ToDto());
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Verify([FromBody] VerifyRequest request)
        {
            var result = await _authService.VerifyAsync(request.ToDto());
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _authService.LoginAsync(request.ToDto());
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            var result = await _authService.RefreshTokenAsync(request.ToDto());
            return Ok(result);
        }
    }
}
