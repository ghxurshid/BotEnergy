using AdminApi.Extensions;
using CommonConfiguration.Attributes;
using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Permissions = Domain.Constants.Permissions;

namespace AdminApi.Controllers
{
    /// <summary>
    /// Natural (jismoniy shaxs) foydalanuvchilarini platforma tomonidan boshqarish.
    /// Natural user o'zi ro'yxatdan o'tadi (AuthApi) — bu yerda yaratish yo'q;
    /// faqat ko'rish/qidirish, bloklash/blokdan chiqarish va o'chirish (soft delete).
    /// Ro'yxat faqat Manage uchun; boshqa amallar scope orqali tekshiriladi.
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class NaturalUserController : ControllerBase
    {
        private readonly ICustomerAdminService _service;

        public NaturalUserController(ICustomerAdminService service)
            => _service = service;

        /// <summary>
        /// Barcha jismoniy shaxslar ro'yxati (sahifalash + sort + qidiruv).
        /// </summary>
        /// <remarks>
        /// **Permission:** `CustomerAdmin.GetNatural` (faqat Manage).
        ///
        /// `PaginationParams` bo'yicha bitta ustun sort va barcha maydonlarda ILIKE qidiruv ishlaydi.
        /// </remarks>
        [HttpGet]
        [RequirePermission(Permissions.CustomerAdminGetNatural)]
        [ProducesResponseType(typeof(PagedResult<CustomerUserItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAll([FromQuery] PaginationParams param)
        {
            var result = await _service.GetNaturalAsync(param, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>Jismoniy shaxsni ID bo'yicha olish.</summary>
        [HttpGet("{id}")]
        [RequirePermission(Permissions.CustomerAdminGetById)]
        [ProducesResponseType(typeof(CustomerUserItemDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _service.GetByIdAsync(id, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>Jismoniy shaxsni bloklash (tizimga kira olmaydi).</summary>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.CustomerAdminBlock)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Block(long id)
        {
            var result = await _service.BlockAsync(id, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>Jismoniy shaxsni blokdan chiqarish.</summary>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.CustomerAdminUnblock)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Unblock(long id)
        {
            var result = await _service.UnblockAsync(id, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>Jismoniy shaxsni o'chirish (soft delete).</summary>
        [HttpDelete("{id}")]
        [RequirePermission(Permissions.CustomerAdminDelete)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _service.DeleteAsync(id, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }
    }
}
