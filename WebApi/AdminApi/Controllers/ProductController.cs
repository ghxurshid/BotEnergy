using AdminApi.Extensions;
using Permissions = Domain.Constants.Permissions;
using AdminApi.Filters.ValidationFilters;
using AdminApi.Models.Requests;
using CommonConfiguration.Attributes;
using Domain.Enums;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminApi.Controllers
{
    /// <summary>
    /// Mahsulotlar boshqaruvi.
    /// Qurilma orqali beriladigan mahsulotlarni yaratish va boshqarish.
    ///
    /// **Ierarxiya:** Device → Product → UsageSession
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _service;

        public ProductController(IProductService service)
            => _service = service;

        /// <summary>
        /// Qurilma turiga ruxsat berilgan mahsulot turlarini olish.
        /// </summary>
        [HttpGet]
        [RequirePermission(Permissions.ProductAdminGetAllowedTypes)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetAllowedTypes([FromQuery] DeviceType deviceType)
        {
            var result = _service.GetAllowedProductTypes(deviceType);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Yangi mahsulot yaratish va qurilmaga biriktirish.
        /// </summary>
        [HttpPost]
        [RequirePermission(Permissions.ProductAdminCreate)]
        [TypeFilter(typeof(CreateProductValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
        {
            var result = await _service.CreateAsync(request.ToDto(), User.GetUserId(), User.GetPermissions());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Barcha mahsulotlar ro'yxati.
        /// </summary>
        [HttpGet]
        [RequirePermission(Permissions.ProductAdminGetAll)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(result.Result);
        }

        /// <summary>
        /// Qurilmaga tegishli mahsulotlar ro'yxati.
        /// </summary>
        [HttpGet("by-device/{deviceId}")]
        [RequirePermission(Permissions.ProductAdminGetByDevice)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetByDevice(long deviceId)
        {
            var result = await _service.GetByDeviceAsync(deviceId);
            return Ok(result.Result);
        }

        /// <summary>
        /// Mahsulotni ID bo'yicha olish.
        /// </summary>
        [HttpGet("{id}")]
        [RequirePermission(Permissions.ProductAdminGetById)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _service.GetByIdAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Mahsulot ma'lumotlarini yangilash (faqat Name, Description, Price, IsActive).
        /// </summary>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.ProductAdminUpdate)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateProductRequest request)
        {
            var result = await _service.UpdateAsync(id, request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Mahsulotni o'chirish (soft delete).
        /// </summary>
        [HttpDelete("{id}")]
        [RequirePermission(Permissions.ProductAdminDelete)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _service.DeleteAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }
    }
}
