using Domain.Dtos;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        public async Task<IActionResult> Me()
        {
            var phoneNumber = User.Identity?.Name;
            if (string.IsNullOrEmpty(phoneNumber))
                return Unauthorized();

            var result = await _userService.GetCurrentUserAsync(phoneNumber);
            if (!result.IsSuccess)
                return NotFound(result);

            return Ok(result);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateMe([FromBody] UpdateMeRequest request)
        {
            var phoneNumber = User.Identity?.Name;
            if (string.IsNullOrEmpty(phoneNumber))
                return Unauthorized();

            var dto = new UpdateUserDto
            {
                Mail = request.Mail,
                PhoneId = request.PhoneId
            };

            var result = await _userService.UpdateCurrentUserAsync(phoneNumber, dto);
            if (!result.IsSuccess)
                return NotFound(result);

            return Ok(result);
        }
    }
}
