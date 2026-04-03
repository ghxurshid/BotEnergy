using CommonConfiguration.Attributes;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UserApi.Extensions;
using UserApi.Models.Requests;

namespace UserApi.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class UserController(IUserService userService) : ControllerBase
    {
        private readonly IUserService _userService = userService;

        [HttpGet]
        [SkipPermissionCheck]
        public async Task<IActionResult> Me()
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _userService.GetCurrentUserAsync(userId);
            if (!result.IsSuccess)
                return NotFound(result);

            return Ok(result);
        }

        [HttpPut]
        [SkipPermissionCheck]
        public async Task<IActionResult> UpdateMe([FromBody] UpdateMeRequest request)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _userService.UpdateCurrentUserAsync(userId, request.ToDto());
            if (!result.IsSuccess)
                return NotFound(result);

            return Ok(result);
        }

        private bool TryGetUserId(out long userId)
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return long.TryParse(raw, out userId);
        }
    }
}
