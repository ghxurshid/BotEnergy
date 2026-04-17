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
    /// Merchantlar (yuridik mijozlar) boshqaruvi.
    /// Shartnoma asosida ishlayotgan kompaniya-merchantlarni ro'yxatdan o'tkazish va boshqarish.
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class MerchantController : ControllerBase
    {
        private readonly IMerchantService _service;

        public MerchantController(IMerchantService service)
            => _service = service;

        /// <summary>
        /// Yangi merchant ro'yxatdan o'tkazish.
        /// </summary>
        [HttpPost]
        [RequirePermission(Permissions.MerchantAdminRegister)]
        [TypeFilter(typeof(RegisterMerchantValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Register([FromBody] RegisterMerchantRequest request)
        {
            var result = await _service.CreateAsync(request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Barcha merchantlar ro'yxati.
        /// </summary>
        [HttpGet]
        [RequirePermission(Permissions.MerchantAdminGetAll)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(result.Result);
        }

        /// <summary>
        /// Merchantni ID bo'yicha olish.
        /// </summary>
        [HttpGet("{id}")]
        [RequirePermission(Permissions.MerchantAdminGetById)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _service.GetByIdAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Merchant ma'lumotlarini yangilash (faqat PhoneNumber).
        /// </summary>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.MerchantAdminUpdate)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateMerchantRequest request)
        {
            var result = await _service.UpdateAsync(id, request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Merchantni o'chirish (soft delete).
        /// </summary>
        [HttpDelete("{id}")]
        [RequirePermission(Permissions.MerchantAdminDelete)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _service.DeleteAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }
    }
}
