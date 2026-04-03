using CommonConfiguration.Attributes;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UserApi.Extensions;
using UserApi.Models.Requests;

namespace UserApi.Controllers
{
    /// <summary>
    /// Foydalanuvchi profili boshqaruvi.
    /// Avtorizatsiya talab qilinadi (JWT Bearer token).
    ///
    /// **Imkoniyatlar:**
    /// - O'z profilini ko'rish (Me)
    /// - Profilni yangilash (UpdateMe)
    ///
    /// **Cheklovlar:**
    /// - Faqat o'z profilingiz bilan ishlaysiz — boshqa foydalanuvchi ma'lumotlarini ko'rish/o'zgartirish mumkin emas
    /// - JWT token Header da `Authorization: Bearer {token}` shaklida yuborilishi shart
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class UserController(IUserService userService) : ControllerBase
    {
        private readonly IUserService _userService = userService;

        /// <summary>
        /// Joriy foydalanuvchi profilini olish.
        /// JWT tokendagi user_id bo'yicha foydalanuvchi ma'lumotlari qaytariladi.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     GET /api/User/Me
        ///     Headers: Authorization: Bearer eyJhbGciOiJI...
        ///
        /// **Javobda qaytadi:**
        /// - Ism, telefon raqam, email, balans, user_type (natural/legal), rol va h.k.
        /// </remarks>
        /// <response code="200">Foydalanuvchi ma'lumotlari</response>
        /// <response code="401">Token yo'q yoki yaroqsiz</response>
        /// <response code="404">Foydalanuvchi topilmadi</response>
        [HttpGet]
        [SkipPermissionCheck]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Me()
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _userService.GetCurrentUserAsync(userId);
            if (!result.IsSuccess)
                return NotFound(result);

            return Ok(result);
        }

        /// <summary>
        /// Joriy foydalanuvchi profilini yangilash.
        /// Faqat email va qurilma identifikatorini o'zgartirish mumkin.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     PUT /api/User/UpdateMe
        ///     Headers: Authorization: Bearer eyJhbGciOiJI...
        ///     {
        ///         "mail": "newemail@example.com",
        ///         "phoneId": "new-device-uuid"
        ///     }
        ///
        /// **Eslatma:** Faqat yuborilgan maydonlar yangilanadi. `null` qoldirsa o'zgarmaydi.
        /// </remarks>
        /// <param name="request">Yangilanadigan maydonlar</param>
        /// <response code="200">Profil yangilandi</response>
        /// <response code="401">Token yo'q yoki yaroqsiz</response>
        [HttpPut]
        [SkipPermissionCheck]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
