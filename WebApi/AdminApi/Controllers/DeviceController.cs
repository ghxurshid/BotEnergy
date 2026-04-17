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
    /// IoT qurilmalar boshqaruvi (admin panel).
    /// Qurilmalarni ro'yxatdan o'tkazish, ko'rish, yangilash va o'chirish.
    ///
    /// **Ierarxiya:** Organization → Station → Device → Product
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class DeviceController : ControllerBase
    {
        private readonly IDeviceService _service;

        public DeviceController(IDeviceService service)
            => _service = service;

        /// <summary>
        /// Yangi qurilmani ro'yxatdan o'tkazish.
        /// </summary>
        [HttpPost]
        [RequirePermission(Permissions.DeviceAdminRegister)]
        [TypeFilter(typeof(RegisterDeviceValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Register([FromBody] RegisterDeviceRequest request)
        {
            var result = await _service.RegisterAsync(request.ToDto(), User.GetUserId(), User.GetPermissions());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Barcha qurilmalar ro'yxati.
        /// </summary>
        [HttpGet]
        [RequirePermission(Permissions.DeviceAdminGetAll)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(result.Result);
        }

        /// <summary>
        /// Qurilmani ID bo'yicha olish.
        /// </summary>
        [HttpGet("{id}")]
        [RequirePermission(Permissions.DeviceAdminGetById)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _service.GetByIdAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Stansiyaga tegishli qurilmalar ro'yxati.
        /// </summary>
        [HttpGet("by-station/{stationId}")]
        [RequirePermission(Permissions.DeviceAdminGetByStation)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetByStation(long stationId)
        {
            var result = await _service.GetByStationAsync(stationId);
            return Ok(result.Result);
        }

        /// <summary>
        /// Qurilma ma'lumotlarini yangilash (faqat Model, FirmwareVersion, IsOnline, IsActive).
        /// </summary>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.DeviceAdminUpdate)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateDeviceRequest request)
        {
            var result = await _service.UpdateAsync(id, request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Qurilmani o'chirish (soft delete).
        /// </summary>
        [HttpDelete("{id}")]
        [RequirePermission(Permissions.DeviceAdminDelete)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _service.DeleteAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }
    }
}
