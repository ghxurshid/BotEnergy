using AdminApi.Extensions;
using AdminApi.Filters.PermissionFilters;
using AdminApi.Filters.ValidationFilters;
using AdminApi.Models.Requests;
using CommonConfiguration.Attributes;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminApi.Controllers
{
    /// <summary>
    /// Stansiyalar boshqaruvi.
    /// Stansiya — bir nechta qurilmalar joylashgan fizik lokatsiya (yoqilg'i stansiyasi, zaryadlash stantsiyasi va h.k.).
    ///
    /// **Imkoniyatlar:**
    /// - Stansiya yaratish, ko'rish, yangilash, o'chirish (CRUD)
    /// - Tashkilot bo'yicha filtrlash
    ///
    /// **Ierarxiya:** Organization → Station → Device → Product
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class StationAdminController : ControllerBase
    {
        private readonly IStationService _service;

        public StationAdminController(IStationService service)
            => _service = service;

        /// <summary>
        /// Yangi stansiya yaratish.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/StationAdmin/Create
        ///     {
        ///         "name": "Tashkent-1 Stansiyasi",
        ///         "location": "41.2995, 69.2401",
        ///         "organizationId": 1
        ///     }
        /// </remarks>
        /// <param name="request">Stansiya nomi, lokatsiyasi va tashkilot ID</param>
        /// <response code="200">Stansiya yaratildi</response>
        [HttpPost]
        [RequirePermission(Permissions.StationAdminCreate)]
        [TypeFilter(typeof(CreateStationValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Create([FromBody] CreateStationRequest request)
        {
            var result = await _service.CreateAsync(request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Barcha stansiyalar ro'yxati.
        /// </summary>
        /// <response code="200">Stansiyalar ro'yxati</response>
        [HttpGet]
        [RequirePermission(Permissions.StationAdminGetAll)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(result.Result);
        }

        /// <summary>
        /// Stansiyani ID bo'yicha olish.
        /// </summary>
        /// <param name="id">Stansiya ID. Masalan: 1</param>
        /// <response code="200">Stansiya ma'lumotlari</response>
        /// <response code="404">Stansiya topilmadi</response>
        [HttpGet("{id}")]
        [RequirePermission(Permissions.StationAdminGetById)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _service.GetByIdAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Tashkilotga tegishli stansiyalar ro'yxati.
        /// </summary>
        /// <param name="organizationId">Tashkilot ID. Masalan: 1</param>
        /// <response code="200">Shu tashkilotdagi stansiyalar</response>
        [HttpGet("by-organization/{organizationId}")]
        [RequirePermission(Permissions.StationAdminGetByOrganization)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetByOrganization(long organizationId)
        {
            var result = await _service.GetByOrganizationAsync(organizationId);
            return Ok(result.Result);
        }

        /// <summary>
        /// Stansiya ma'lumotlarini yangilash.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     PUT /api/StationAdmin/Update/1
        ///     {
        ///         "name": "Tashkent-1 (yangilangan)",
        ///         "location": "41.3000, 69.2500",
        ///         "isActive": false
        ///     }
        /// </remarks>
        /// <param name="id">Yangilanadigan stansiya ID</param>
        /// <param name="request">Yangilanadigan maydonlar</param>
        /// <response code="200">Stansiya yangilandi</response>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.StationAdminUpdate)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateStationRequest request)
        {
            var result = await _service.UpdateAsync(id, request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Stansiyani o'chirish (soft delete).
        /// </summary>
        /// <param name="id">O'chiriladigan stansiya ID. Masalan: 1</param>
        /// <response code="200">Stansiya o'chirildi</response>
        [HttpDelete("{id}")]
        [RequirePermission(Permissions.StationAdminDelete)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _service.DeleteAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }
    }
}
