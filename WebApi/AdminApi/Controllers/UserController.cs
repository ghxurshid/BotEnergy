using AdminApi.Extensions;
using Permissions = Domain.Constants.Permissions;
using AdminApi.Filters.ValidationFilters;
using AdminApi.Models.Requests;
using CommonConfiguration.Attributes;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminApi.Controllers
{
    /// <summary>
    /// Foydalanuvchilarni admin tomonidan boshqarish.
    /// Barcha turdagi foydalanuvchilar (NaturalUser, LegalUser, MerchantUser) ustida CRUD operatsiyalari.
    ///
    /// **Ierarxiya:** Organization → Station → MerchantUser
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserAdminService _service;

        public UserController(IUserAdminService service)
            => _service = service;

        /// <summary>
        /// Yangi foydalanuvchi yaratish.
        /// OrganizationId berilsa — LegalUser, StationId berilsa — MerchantUser yaratiladi.
        /// </summary>
        [HttpPost]
        [RequirePermission(Permissions.UserAdminCreate)]
        [TypeFilter(typeof(CreateUserValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
        {
            var result = await _service.CreateAsync(request.ToDto(), User.GetUserId(), User.GetPermissions());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Barcha foydalanuvchilar ro'yxati.
        /// </summary>
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
        /// Foydalanuvchiga parol o'rnatish.
        /// </summary>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.UserAdminSetPassword)]
        [TypeFilter(typeof(SetPasswordValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> SetPassword(long id, [FromBody] SetPasswordRequest request)
        {
            var result = await _service.SetPasswordAsync(request.ToDto(id));
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Foydalanuvchi parolini qayta o'rnatish.
        /// </summary>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.UserAdminResetPassword)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ResetPassword(long id, [FromBody] ResetPasswordRequest request)
        {
            var result = await _service.ResetPasswordAsync(request.ToDto(id));
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Foydalanuvchini bloklash.
        /// Bloklangan foydalanuvchi tizimga kira olmaydi.
        /// </summary>
        [HttpPut("{id}")]
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
        [HttpPut("{id}")]
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
