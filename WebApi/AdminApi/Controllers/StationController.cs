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
    /// Stansiyalar boshqaruvi.
    /// Stansiya — bir nechta qurilmalar joylashgan fizik lokatsiya.
    ///
    /// **Ierarxiya:** Organization → Station → Device → Product
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class StationController : ControllerBase
    {
        private readonly IStationService _service;

        public StationController(IStationService service)
            => _service = service;

        /// <summary>
        /// Yangi stansiya yaratish.
        /// </summary>
        [HttpPost]
        [RequirePermission(Permissions.StationAdminCreate)]
        [TypeFilter(typeof(CreateStationValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Create([FromBody] CreateStationRequest request)
        {
            var result = await _service.CreateAsync(request.ToDto(), User.GetUserId(), User.GetPermissions());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Barcha stansiyalar ro'yxati.
        /// </summary>
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
        [HttpGet("by-organization/{organizationId}")]
        [RequirePermission(Permissions.StationAdminGetByOrganization)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetByOrganization(long organizationId)
        {
            var result = await _service.GetByOrganizationAsync(organizationId);
            return Ok(result.Result);
        }

        /// <summary>
        /// Stansiya ma'lumotlarini yangilash (faqat Name, Location, IsActive).
        /// </summary>
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
