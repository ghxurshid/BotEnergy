using Permissions = Domain.Constants.Permissions;
using CommonConfiguration.Attributes;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminApi.Controllers
{
    /// <summary>
    /// Foydalanuvchilarni admin tomonidan boshqarish.
    /// Barcha turdagi foydalanuvchilar (NaturalUser va LegalUser) ustida CRUD operatsiyalari.
    ///
    /// **Imkoniyatlar:**
    /// - Barcha foydalanuvchilarni ko'rish
    /// - ID bo'yicha foydalanuvchi ma'lumotlarini olish
    /// - Foydalanuvchini bloklash / blokdan chiqarish
    /// - Foydalanuvchini o'chirish (soft delete)
    ///
    /// **Cheklovlar:**
    /// - JWT token talab qilinadi
    /// - Bloklangan foydalanuvchi tizimga kira olmaydi
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class UserAdminController : ControllerBase
    {
        private readonly IUserAdminService _service;

        public UserAdminController(IUserAdminService service)
            => _service = service;

        /// <summary>
        /// Barcha foydalanuvchilar ro'yxati.
        /// NaturalUser va LegalUser larni qaytaradi.
        /// </summary>
        /// <response code="200">Foydalanuvchilar ro'yxati</response>
        [HttpGet]
        [RequirePermission(Permissions.UserAdminGetAll)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(result.Result);
        }

        /// <summary>
        /// Foydalanuvchini ID bo'yicha olish.
        /// </summary>
        /// <param name="id">Foydalanuvchi ID. Masalan: 1</param>
        /// <response code="200">Foydalanuvchi ma'lumotlari</response>
        /// <response code="404">Foydalanuvchi topilmadi</response>
        [HttpGet("{id}")]
        [RequirePermission(Permissions.UserAdminGetById)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _service.GetByIdAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Foydalanuvchini bloklash.
        /// Bloklangan foydalanuvchi tizimga kira olmaydi.
        /// </summary>
        /// <param name="id">Bloklanadigan foydalanuvchi ID. Masalan: 5</param>
        /// <response code="200">Foydalanuvchi bloklandi</response>
        /// <response code="404">Foydalanuvchi topilmadi</response>
        [HttpPut("{id}/block")]
        [RequirePermission(Permissions.UserAdminBlock)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Block(long id)
        {
            var result = await _service.BlockAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Foydalanuvchini blokdan chiqarish.
        /// </summary>
        /// <param name="id">Blokdan chiqariladigan foydalanuvchi ID. Masalan: 5</param>
        /// <response code="200">Foydalanuvchi blokdan chiqarildi</response>
        /// <response code="404">Foydalanuvchi topilmadi</response>
        [HttpPut("{id}/unblock")]
        [RequirePermission(Permissions.UserAdminUnblock)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Unblock(long id)
        {
            var result = await _service.UnblockAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Foydalanuvchini o'chirish (soft delete).
        /// </summary>
        /// <param name="id">O'chiriladigan foydalanuvchi ID. Masalan: 5</param>
        /// <response code="200">Foydalanuvchi o'chirildi</response>
        /// <response code="404">Foydalanuvchi topilmadi</response>
        [HttpDelete("{id}")]
        [RequirePermission(Permissions.UserAdminDelete)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _service.DeleteAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }
    }
}
