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
    /// IoT qurilmalar boshqaruvi (admin panel).
    /// Qurilmalarni ro'yxatdan o'tkazish, ko'rish, yangilash va o'chirish.
    ///
    /// **Imkoniyatlar:**
    /// - Yangi qurilma ro'yxatdan o'tkazish (serial number, turi, stansiya)
    /// - Barcha qurilmalarni yoki stansiya bo'yicha ko'rish
    /// - Qurilma ma'lumotlarini yangilash (model, firmware, holat)
    /// - Qurilmani o'chirish
    ///
    /// **Qurilma turlari (DeviceType):**
    /// FuelDispenser, ChargingStation, WaterPump, GasStation va boshqalar
    ///
    /// **Ierarxiya:** Organization → Station → Device → Product
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class DeviceAdminController : ControllerBase
    {
        private readonly IDeviceService _service;

        public DeviceAdminController(IDeviceService service)
            => _service = service;

        /// <summary>
        /// Yangi qurilmani ro'yxatdan o'tkazish.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/DeviceAdmin/Register
        ///     {
        ///         "serialNumber": "SN-2024-001",
        ///         "deviceType": 0,
        ///         "stationId": 1,
        ///         "model": "FD-500",
        ///         "firmwareVersion": "v2.1.0",
        ///         "functionCount": 2
        ///     }
        ///
        /// **deviceType qiymatlari:** 0=FuelDispenser, 1=ChargingStation, 2=WaterPump, ...
        /// **functionCount** — qurilmadagi funksiyalar soni (masalan, 2 ta nasos)
        /// </remarks>
        /// <param name="request">Qurilma ma'lumotlari</param>
        /// <response code="200">Qurilma ro'yxatdan o'tkazildi</response>
        [HttpPost]
        [RequirePermission(Permissions.DeviceAdminRegister)]
        [TypeFilter(typeof(RegisterDeviceValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Register([FromBody] RegisterDeviceRequest request)
        {
            var result = await _service.RegisterAsync(request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Barcha qurilmalar ro'yxati.
        /// </summary>
        /// <response code="200">Qurilmalar ro'yxati</response>
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
        /// <param name="id">Qurilma ID. Masalan: 1</param>
        /// <response code="200">Qurilma ma'lumotlari</response>
        /// <response code="404">Qurilma topilmadi</response>
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
        /// <param name="stationId">Stansiya ID. Masalan: 1</param>
        /// <response code="200">Shu stansiyadagi qurilmalar</response>
        [HttpGet("by-station/{stationId}")]
        [RequirePermission(Permissions.DeviceAdminGetByStation)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetByStation(long stationId)
        {
            var result = await _service.GetByStationAsync(stationId);
            return Ok(result.Result);
        }

        /// <summary>
        /// Qurilma ma'lumotlarini yangilash.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     PUT /api/DeviceAdmin/Update/1
        ///     {
        ///         "model": "FD-600",
        ///         "firmwareVersion": "v3.0.0",
        ///         "isActive": true,
        ///         "stationId": 2
        ///     }
        ///
        /// Faqat yuborilgan maydonlar yangilanadi. `null` qoldirilsa o'zgarmaydi.
        /// </remarks>
        /// <param name="id">Yangilanadigan qurilma ID</param>
        /// <param name="request">Yangilanadigan maydonlar</param>
        /// <response code="200">Qurilma yangilandi</response>
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
        /// <param name="id">O'chiriladigan qurilma ID. Masalan: 1</param>
        /// <response code="200">Qurilma o'chirildi</response>
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
