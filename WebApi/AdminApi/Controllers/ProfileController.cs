using AdminApi.Extensions;
using AdminApi.Models.Requests;
using CommonConfiguration.Attributes;
using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminApi.Controllers
{
    /// <summary>
    /// Joriy (login qilgan) platform userning O'Z profili.
    /// Boshqa userlarni boshqarish `/api/User/*` (permission talab qiladi) da — bu yerda esa
    /// har bir authenticated user faqat o'ziga tegishli amallarni bajaradi (permission shart emas).
    ///
    /// **Imkoniyatlar (hozircha):**
    /// - `Me` — o'z profilini ko'rish.
    /// - `UpdateEmail` — o'z emailini yangilash.
    /// - `ChangePassword` — o'z parolini o'zgartirish (joriy parol tasdiqlanadi).
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly IPlatformProfileService _service;

        public ProfileController(IPlatformProfileService service)
            => _service = service;

        /// <summary>
        /// Joriy user o'z profilini oladi (JWT'dagi user_id bo'yicha).
        /// </summary>
        /// <response code="200">Profil ma'lumotlari.</response>
        /// <response code="401">Token yo'q yoki yaroqsiz.</response>
        /// <response code="404">Foydalanuvchi topilmadi.</response>
        [HttpGet]
        [SkipPermissionCheck]
        [ProducesResponseType(typeof(MyProfileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Me()
        {
            var result = await _service.GetMeAsync(User.GetUserId());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Joriy user o'z emailini yangilaydi.
        /// </summary>
        /// <remarks>
        /// **Request body:** `{ "mail": "yangi@example.com" }`
        /// </remarks>
        /// <response code="200">Email yangilandi — yangilangan profil qaytadi.</response>
        /// <response code="400">Email formati noto'g'ri.</response>
        /// <response code="401">Token yo'q yoki yaroqsiz.</response>
        [HttpPut]
        [SkipPermissionCheck]
        [ProducesResponseType(typeof(MyProfileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateEmail([FromBody] UpdateEmailRequest request)
        {
            var result = await _service.UpdateEmailAsync(User.GetUserId(), request.Mail);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Joriy user o'z parolini o'zgartiradi.
        /// </summary>
        /// <remarks>
        /// **Request body:** `{ "currentPassword": "...", "newPassword": "..." }`
        ///
        /// Joriy parol backendda tekshiriladi — mos kelmasa parol o'zgartirilmaydi (403).
        /// </remarks>
        /// <response code="200">Parol o'zgartirildi.</response>
        /// <response code="400">Yangi parol yaroqsiz yoki joriy parol kiritilmagan.</response>
        /// <response code="401">Token yo'q yoki yaroqsiz.</response>
        /// <response code="403">Joriy parol noto'g'ri.</response>
        [HttpPut]
        [SkipPermissionCheck]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var result = await _service.ChangePasswordAsync(User.GetUserId(), request.CurrentPassword, request.NewPassword);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }
    }
}
