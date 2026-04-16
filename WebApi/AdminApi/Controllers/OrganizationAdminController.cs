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
    /// Tashkilotlar (Organization) boshqaruvi.
    /// Tashkilot — yuridik foydalanuvchilar va stansiyalarni guruhlash uchun ishlatiladi.
    ///
    /// **Imkoniyatlar:**
    /// - Tashkilot yaratish, ko'rish, yangilash, o'chirish (CRUD)
    ///
    /// **Bog'liqliklar:**
    /// - Tashkilotga stansiyalar biriktiriladi (StationAdmin)
    /// - LegalUser tashkilotga tegishli — balans tashkilot darajasida saqlanadi
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class OrganizationAdminController : ControllerBase
    {
        private readonly IOrganizationService _service;

        public OrganizationAdminController(IOrganizationService service)
            => _service = service;

        /// <summary>
        /// Yangi tashkilot yaratish.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/OrganizationAdmin/Create
        ///     {
        ///         "name": "BotEnergy LLC",
        ///         "inn": "123456789",
        ///         "address": "Tashkent, Amir Temur 1",
        ///         "phoneNumber": "998712345678"
        ///     }
        /// </remarks>
        /// <param name="request">Tashkilot ma'lumotlari</param>
        /// <response code="200">Tashkilot yaratildi</response>
        [HttpPost]
        [RequirePermission(Permissions.OrganizationAdminCreate)]
        [TypeFilter(typeof(CreateOrganizationValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Create([FromBody] CreateOrganizationRequest request)
        {
            var result = await _service.CreateAsync(request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Barcha tashkilotlar ro'yxati.
        /// </summary>
        /// <response code="200">Tashkilotlar ro'yxati</response>
        [HttpGet]
        [RequirePermission(Permissions.OrganizationAdminGetAll)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(result.Result);
        }

        /// <summary>
        /// Tashkilotni ID bo'yicha olish.
        /// </summary>
        /// <param name="id">Tashkilot ID. Masalan: 1</param>
        /// <response code="200">Tashkilot ma'lumotlari</response>
        /// <response code="404">Tashkilot topilmadi</response>
        [HttpGet("{id}")]
        [RequirePermission(Permissions.OrganizationAdminGetById)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _service.GetByIdAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Tashkilot ma'lumotlarini yangilash.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     PUT /api/OrganizationAdmin/Update/1
        ///     {
        ///         "name": "BotEnergy Group",
        ///         "address": "Tashkent, Navoi 15"
        ///     }
        ///
        /// Faqat yuborilgan maydonlar yangilanadi.
        /// </remarks>
        /// <param name="id">Yangilanadigan tashkilot ID</param>
        /// <param name="request">Yangilanadigan maydonlar</param>
        /// <response code="200">Tashkilot yangilandi</response>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.OrganizationAdminUpdate)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateOrganizationRequest request)
        {
            var result = await _service.UpdateAsync(id, request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Tashkilotni o'chirish (soft delete).
        /// </summary>
        /// <param name="id">O'chiriladigan tashkilot ID. Masalan: 1</param>
        /// <response code="200">Tashkilot o'chirildi</response>
        [HttpDelete("{id}")]
        [RequirePermission(Permissions.OrganizationAdminDelete)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _service.DeleteAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }
    }
}
