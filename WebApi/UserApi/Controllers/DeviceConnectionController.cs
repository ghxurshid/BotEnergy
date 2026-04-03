using CommonConfiguration.Attributes;
using Domain.Interfaces;
using Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UserApi.Controllers
{
    /// <summary>
    /// Foydalanuvchi qurilma bilan ulanishdan oldin uning mahsulotlarini ko'radi.
    /// QR kod skanerlanganidan keyin serial number bo'yicha mavjud productlar qaytariladi.
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class DeviceConnectionController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly IDeviceRepository _deviceRepository;

        public DeviceConnectionController(IProductService productService, IDeviceRepository deviceRepository)
        {
            _productService = productService;
            _deviceRepository = deviceRepository;
        }

        /// <summary>
        /// QR kod orqali qurilmaning mavjud mahsulotlarini ko'rish.
        /// Serial number: QR kodda joylashgan qurilma identifikatori.
        /// </summary>
        [HttpGet("{serialNumber}")]
        [SkipPermissionCheck]
        public async Task<IActionResult> GetProducts(string serialNumber)
        {
            var device = await _deviceRepository.GetBySerialNumberAsync(serialNumber);
            if (device is null)
                return NotFound(new { message = "Qurilma topilmadi yoki faol emas." });

            var result = _productService.GetAllowedProductTypes(device.DeviceType);

            return Ok(new
            {
                device_id = device.Id,
                serial_number = device.SerialNumber,
                device_type = device.DeviceType.ToString(),
                station = device.Station?.Name,
                allowed_product_types = result.Result?.AllowedProductTypes
            });
        }
    }
}
