using AdminApi.Extensions;
using Permissions = Domain.Constants.Permissions;
using AdminApi.Filters.ValidationFilters;
using AdminApi.Models.Requests;
using CommonConfiguration.Attributes;
using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Enums;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminApi.Controllers
{
    /// <summary>
    /// Mahsulotlar boshqaruvi.
    /// </summary>
    /// <remarks>
    /// Mahsulot (Product) — merchant qurilmasi orqali beriladigan xizmat yoki tovar. Har bir mahsulot bitta qurilmaga biriktiriladi.
    ///
    /// **Ierarxiya:** Merchant → Station → Device → Product → UsageSession
    ///
    /// **Permission level:**
    /// - `station.*` permissionigacha bo'lgan user — faqat o'ziga tegishli stansiyalardagi qurilmalarga mahsulot qo'sha oladi.
    /// - `merchant.*` permissioniga ega user — boshqa merchantlardagi stansiyalardagi qurilmalar uchun ham mahsulot qo'sha oladi.
    ///
    /// Barcha endpointlar JWT token va tegishli permission talab qiladi.
    /// Xatolik bo'lsa response body'da `{ "message": "..." }` formatida sabab qaytariladi.
    /// </remarks>
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
        /// <remarks>
        /// Berilgan qurilma turiga mos keluvchi mahsulot turlarini qaytaradi.
        /// Mahsulot yaratishdan oldin qaysi turlar mavjudligini tekshirish uchun ishlatiladi.
        ///
        /// **Permission:** `product.admin.getallowedtypes`
        /// </remarks>
        /// <param name="deviceType">Qurilma turi (enum).</param>
        /// <response code="200">Ruxsat berilgan mahsulot turlari ro'yxati.</response>
        /// <response code="403">Permission yetarli emas.</response>
        [HttpGet]
        [RequirePermission(Permissions.ProductAdminGetAllowedTypes)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public IActionResult GetAllowedTypes([FromQuery] DeviceType deviceType)
        {
            var result = _service.GetAllowedProductTypes(deviceType);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Yangi mahsulot yaratish va qurilmaga biriktirish.
        /// </summary>
        /// <remarks>
        /// Yangi mahsulotni tizimga qo'shadi va ko'rsatilgan qurilmaga biriktiradi.
        ///
        /// **Permission:** `product.admin.create`
        ///
        /// **Permission level:** User faqat o'ziga ruxsat berilgan qurilmalarga mahsulot qo'sha oladi:
        /// - `station.*` permissionigacha — faqat o'z stansiyasidagi qurilmalar uchun.
        /// - `merchant.*` permissioni — boshqa merchantlardagi stansiyalardagi qurilmalar uchun ham.
        ///
        /// **Request body maydonlari:**
        ///
        /// | Maydon      | Turi    | Majburiy | ReadOnly | Tavsif                                                                                                               |
        /// |-------------|---------|----------|----------|----------------------------------------------------------------------------------------------------------------------|
        /// | Name        | string  | **Ha**   | Yo'q     | Mahsulot nomi. Keyinchalik o'zgartirish mumkin.                                                                      |
        /// | Description | string  | Yo'q     | Yo'q     | Mahsulot tavsifi (ixtiyoriy).                                                                                        |
        /// | ProductType | enum    | **Ha**   | Ha       | Mahsulot turi. Yaratilgandan keyin o'zgartirilmaydi. `GetAllowedTypes` orqali ruxsat berilgan turlarni olish mumkin. |
        /// | Unit        | enum    | **Ha**   | Ha       | O'lchov birligi (litr, kg, dona va h.k.). Yaratilgandan keyin o'zgartirilmaydi.                                      |
        /// | Price       | decimal | **Ha**   | Yo'q     | Mahsulot narxi. Keyinchalik o'zgartirish mumkin.                                                                     |
        /// | DeviceId    | long    | **Ha**   | Ha       | Mahsulot biriktirilgan qurilma ID si. Yaratilgandan keyin o'zgartirilmaydi.                                          |
        /// | IsActive    | bool    | Yo'q     | Yo'q     | Faol holati. Berilmasa default (true).                                                                               |
        ///
        /// **Xatolik holatlari:**
        /// - Ko'rsatilgan `DeviceId` bo'yicha qurilma topilmasa — xatolik qaytadi.
        /// - User ko'rsatilgan qurilmaga mahsulot qo'shish huquqiga ega bo'lmasa — xatolik qaytadi.
        /// </remarks>
        /// <param name="request">Mahsulot yaratish uchun ma'lumotlar.</param>
        /// <response code="200">Mahsulot muvaffaqiyatli yaratildi.</response>
        /// <response code="400">Validatsiya xatosi (majburiy maydonlar to'ldirilmagan).</response>
        /// <response code="403">Permission yetarli emas yoki qurilmaga ruxsat yo'q.</response>
        /// <response code="404">Ko'rsatilgan DeviceId bo'yicha qurilma topilmadi.</response>
        [HttpPost]
        [RequirePermission(Permissions.ProductAdminCreate)]
        [TypeFilter(typeof(CreateProductValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
        {
            var result = await _service.CreateAsync(request.ToDto(), User.GetUserId(), User.GetPermissions());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Mahsulotlar ro'yxatini sahifalab olish.
        /// </summary>
        /// <remarks>
        /// Tizimdagi mahsulotlarni sahifalab qaytaradi (soft delete qilinganlar bundan mustasno).
        ///
        /// **Permission:** `product.admin.getall`
        ///
        /// **Query parametrlari:**
        ///
        /// | Maydon     | Turi | Majburiy | Default | Tavsif                                                           |
        /// |------------|------|----------|---------|------------------------------------------------------------------|
        /// | PageNumber | int  | Yo'q     | 1       | Sahifa raqami (1 dan boshlanadi).                                |
        /// | PageSize   | int  | Yo'q     | 20      | Bir sahifadagi yozuvlar soni. Maksimal 100 gacha cheklanadi.     |
        ///
        /// **Response:** `items` bilan birga `pageNumber`, `pageSize`, `totalCount`, `totalPages`, `hasNext`, `hasPrevious` qaytariladi.
        /// </remarks>
        /// <param name="param">Sahifalash parametrlari.</param>
        /// <response code="200">Mahsulotlar ro'yxati muvaffaqiyatli qaytarildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        [HttpGet]
        [RequirePermission(Permissions.ProductAdminGetAll)]
        [ProducesResponseType(typeof(PagedResult<ProductItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAll([FromQuery] PaginationParams param)
        {
            var result = await _service.GetAllAsync(param);
            return Ok(result.Result);
        }

        /// <summary>
        /// Qurilmaga tegishli mahsulotlar ro'yxati.
        /// </summary>
        /// <remarks>
        /// Berilgan qurilma ID si bo'yicha unga tegishli barcha mahsulotlarni qaytaradi.
        ///
        /// **Permission:** `product.admin.getbydevice`
        /// </remarks>
        /// <param name="deviceId">Qurilma ID si.</param>
        /// <response code="200">Qurilmaga tegishli mahsulotlar ro'yxati qaytarildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        [HttpGet("by-device/{deviceId}")]
        [RequirePermission(Permissions.ProductAdminGetByDevice)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetByDevice(long deviceId)
        {
            var result = await _service.GetByDeviceAsync(deviceId);
            return Ok(result.Result);
        }

        /// <summary>
        /// Mahsulotni ID bo'yicha olish.
        /// </summary>
        /// <remarks>
        /// Berilgan ID bo'yicha bitta mahsulot ma'lumotlarini qaytaradi.
        ///
        /// **Permission:** `product.admin.getbyid`
        /// </remarks>
        /// <param name="id">Mahsulot ID si.</param>
        /// <response code="200">Mahsulot topildi va qaytarildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Berilgan ID bo'yicha mahsulot topilmadi.</response>
        [HttpGet("{id}")]
        [RequirePermission(Permissions.ProductAdminGetById)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _service.GetByIdAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Mahsulot ma'lumotlarini yangilash.
        /// </summary>
        /// <remarks>
        /// Faqat readonly bo'lmagan maydonlarni yangilash mumkin. ProductType, Unit, DeviceId o'zgartirilmaydi.
        ///
        /// **Permission:** `product.admin.update`
        ///
        /// **Yangilanishi mumkin bo'lgan maydonlar:**
        ///
        /// | Maydon      | Turi     | Tavsif                |
        /// |-------------|----------|-----------------------|
        /// | Name        | string?  | Mahsulot nomi.        |
        /// | Description | string?  | Mahsulot tavsifi.     |
        /// | Price       | decimal? | Mahsulot narxi.       |
        /// | IsActive    | bool?    | Faol holati.          |
        ///
        /// Faqat yuborilgan (null bo'lmagan) maydonlar yangilanadi.
        /// </remarks>
        /// <param name="id">Yangilanadigan mahsulot ID si.</param>
        /// <param name="request">Yangilanadigan maydonlar.</param>
        /// <response code="200">Mahsulot muvaffaqiyatli yangilandi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Berilgan ID bo'yicha mahsulot topilmadi.</response>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.ProductAdminUpdate)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateProductRequest request)
        {
            var result = await _service.UpdateAsync(id, request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Mahsulotni o'chirish (soft delete).
        /// </summary>
        /// <remarks>
        /// Mahsulotni bazadan butunlay o'chirmaydi, `IsDeleted = true` qilib belgilaydi.
        /// O'chirilgan mahsulot ro'yxatlarda ko'rinmaydi.
        ///
        /// **Permission:** `product.admin.delete`
        /// </remarks>
        /// <param name="id">O'chiriladigan mahsulot ID si.</param>
        /// <response code="200">Mahsulot muvaffaqiyatli o'chirildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Berilgan ID bo'yicha mahsulot topilmadi.</response>
        [HttpDelete("{id}")]
        [RequirePermission(Permissions.ProductAdminDelete)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _service.DeleteAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }
    }
}
