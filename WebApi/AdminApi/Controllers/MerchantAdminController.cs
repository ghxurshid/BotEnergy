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
    ///
    /// **Imkoniyatlar:**
    /// - Merchant ro'yxatdan o'tkazish (INN, bank hisob raqami, kompaniya nomi)
    /// - Barcha merchantlarni ko'rish
    /// - ID bo'yicha merchant olish
    /// - Merchant ma'lumotlarini yangilash
    /// - Merchantni o'chirish
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class MerchantAdminController : ControllerBase
    {
        private readonly IMerchantService _service;

        public MerchantAdminController(IMerchantService service)
            => _service = service;

        /// <summary>
        /// Yangi merchant ro'yxatdan o'tkazish.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/MerchantAdmin/Register
        ///     {
        ///         "phoneNumber": "998901234567",
        ///         "inn": "123456789",
        ///         "bankAccount": "20208000900100001001",
        ///         "companyName": "Tech Solutions LLC"
        ///     }
        /// </remarks>
        /// <param name="request">Merchant ma'lumotlari</param>
        /// <response code="200">Merchant ro'yxatdan o'tkazildi</response>
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
        /// <response code="200">Merchantlar ro'yxati</response>
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
        /// <param name="id">Merchant ID. Masalan: 1</param>
        /// <response code="200">Merchant ma'lumotlari</response>
        /// <response code="404">Merchant topilmadi</response>
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
        /// Merchant ma'lumotlarini yangilash.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     PUT /api/MerchantAdmin/Update/1
        ///     {
        ///         "phoneNumber": "998909876543",
        ///         "companyName": "Tech Solutions Group"
        ///     }
        ///
        /// Faqat yuborilgan maydonlar yangilanadi. `null` qoldirilsa o'zgarmaydi.
        /// </remarks>
        /// <param name="id">Yangilanadigan merchant ID</param>
        /// <param name="request">Yangilanadigan maydonlar</param>
        /// <response code="200">Merchant yangilandi</response>
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
        /// <param name="id">O'chiriladigan merchant ID. Masalan: 1</param>
        /// <response code="200">Merchant o'chirildi</response>
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
