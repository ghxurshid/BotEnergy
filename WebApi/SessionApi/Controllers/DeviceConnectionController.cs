using CommonConfiguration.Attributes;
using Domain.Interfaces;
using Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UserApi.Controllers
{
    /// <summary>
    /// Qurilmaga ulanishdan oldingi ma'lumotlar.
    /// Foydalanuvchi QR kodni skanerlaydi → qurilma serial raqami bo'yicha mavjud mahsulotlar ko'rsatiladi.
    ///
    /// **Jarayon:**
    /// 1. Foydalanuvchi ilovada QR kodni skanerlaydi (QR ichida qurilma serial_number bor)
    /// 2. GetProducts → qurilma turidan kelib chiqib ruxsat berilgan mahsulot turlari qaytadi
    /// 3. Foydalanuvchi mahsulotni tanlaydi → UsageSessionApi orqali sessiya yaratadi
    ///
    /// **Cheklovlar:**
    /// - JWT token talab qilinadi
    /// - Faqat faol (is_active=true) qurilmalar uchun ishlaydi
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
        /// QR kod orqali qurilmaning mavjud mahsulotlarini olish.
        /// Serial number QR kodda joylashgan qurilma identifikatori.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     GET /api/DeviceConnection/GetProducts/SN-2024-001
        ///     Headers: Authorization: Bearer eyJhbGciOiJI...
        ///
        /// **Javobda qaytadi:**
        /// - `device_id` — qurilma ID si
        /// - `serial_number` — qurilma seriya raqami
        /// - `device_type` — qurilma turi (FuelDispenser, ChargingStation, WaterPump, ...)
        /// - `station` — stansiya nomi
        /// - `allowed_product_types` — ruxsat berilgan mahsulot turlari ro'yxati
        /// </remarks>
        /// <param name="serialNumber">Qurilma seriya raqami (QR koddan olinadi). Masalan: SN-2024-001</param>
        /// <response code="200">Qurilma va mahsulotlar ro'yxati</response>
        /// <response code="404">Qurilma topilmadi yoki faol emas</response>
        [HttpGet("{serialNumber}")]
        [SkipPermissionCheck]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
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
