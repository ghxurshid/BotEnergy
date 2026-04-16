using AdminApi.Extensions;
using Permissions = Domain.Constants.Permissions;
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
    /// Qurilma orqali beriladigan mahsulotlarni (yoqilg'i, elektr, suv, gaz) yaratish va boshqarish.
    ///
    /// **Imkoniyatlar:**
    /// - Qurilma turiga ruxsat berilgan mahsulot turlarini olish
    /// - Yangi mahsulot yaratish (qurilmaga biriktirish)
    ///
    /// **Ierarxiya:** Device → Product → UsageSession
    ///
    /// **Mahsulot turlari (ProductType):** Fuel, Electricity, Water, Gas va boshqalar
    /// **O'lchov birliklari (UnitType):** Litr, KWh, Kubometr va boshqalar
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class ProductAdminController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductAdminController(IProductService productService)
        {
            _productService = productService;
        }

        /// <summary>
        /// Qurilma turiga ruxsat berilgan mahsulot turlarini olish.
        /// Masalan: FuelDispenser → [Fuel, Gas], ChargingStation → [Electricity]
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     GET /api/ProductAdmin/GetAllowedTypes?deviceType=0
        ///
        /// **deviceType qiymatlari:** 0=FuelDispenser, 1=ChargingStation, 2=WaterPump, ...
        /// </remarks>
        /// <param name="deviceType">Qurilma turi (enum)</param>
        /// <response code="200">Ruxsat berilgan mahsulot turlari</response>
        [HttpGet]
        [RequirePermission(Permissions.ProductAdminGetAllowedTypes)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetAllowedTypes([FromQuery] DeviceType deviceType)
        {
            var result = _productService.GetAllowedProductTypes(deviceType);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, result);
        }

        /// <summary>
        /// Yangi mahsulot yaratish va qurilmaga biriktirish.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/ProductAdmin/Create
        ///     {
        ///         "name": "AI-95 benzin",
        ///         "description": "Premium benzin",
        ///         "productType": 0,
        ///         "unit": 0,
        ///         "price": 12500.00,
        ///         "deviceId": 1
        ///     }
        ///
        /// **productType:** 0=Fuel, 1=Electricity, 2=Water, 3=Gas
        /// **unit:** 0=Litr, 1=KWh, 2=Kubometr
        /// **price:** Bir birlik narxi (so'mda)
        /// </remarks>
        /// <param name="request">Mahsulot ma'lumotlari</param>
        /// <response code="200">Mahsulot yaratildi</response>
        [HttpPost]
        [RequirePermission(Permissions.ProductAdminCreate)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
        {
            var result = await _productService.CreateAsync(request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, result);
        }
    }
}
