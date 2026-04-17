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
    /// </summary>
    /// <remarks>
    /// **Ierarxiya:** Merchant → Station → Device → Product
    ///
    /// Qurilma (Device) — stansiyaga biriktirilgan IoT qurilma. Har bir qurilma bitta stansiyaga tegishli.
    /// Stansiya esa merchantga tegishli. Merchant — platformada mahsulotini sotadigan tashkilot.
    ///
    /// **Permission level:**
    /// - `device.*` permissioniga ega user — faqat o'ziga tegishli merchant stansiyalaridagi qurilmalarga ruxsat.
    /// - `merchant.*` permissioniga ega user — boshqa merchantlardagi stansiyalardagi qurilmalarga ham ruxsat.
    ///
    /// Barcha endpointlar JWT token va tegishli permission talab qiladi.
    /// Xatolik bo'lsa response body'da `{ "message": "..." }` formatida sabab qaytariladi.
    /// </remarks>
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
        /// <remarks>
        /// Yangi IoT qurilmani tizimga qo'shadi va ko'rsatilgan stansiyaga biriktiradi.
        ///
        /// **Permission:** `device.admin.register`
        ///
        /// **Permission level:** Agar user `device.*` permissioniga ega bo'lsa — faqat o'ziga tegishli merchant stansiyalari uchun qurilma qo'sha oladi.
        /// Agar `merchant.*` permissioni bo'lsa — boshqa merchantlar stansiyalari uchun ham qo'sha oladi.
        ///
        /// **Request body maydonlari:**
        ///
        /// | Maydon          | Turi  | Majburiy | ReadOnly | Tavsif                                                                 |
        /// |-----------------|-------|----------|----------|-------------------------------------------------------------------------
        /// | SerialNumber    | string| **Ha**   | Ha       | Qurilmaning seriya raqami. Yaratilgandan keyin o'zgartirilmaydi.      |
        /// | DeviceType      | enum  | **Ha**   | Ha       | Qurilma turi. Yaratilgandan keyin o'zgartirilmaydi.                   |
        /// | StationId       | long  | **Ha**   | Ha       | Qurilma biriktirilgan stansiya ID si. Yaratilgandan keyin o'zgartirilmaydi. |
        /// | Model           | string| Yo'q     | Yo'q     | Qurilma modeli (ixtiyoriy).                                           |
        /// | FirmwareVersion | string| Yo'q     | Yo'q     | Firmware versiyasi (ixtiyoriy).                                       |
        /// | IsOnline        | bool  | Yo'q     | Yo'q     | Online holati. Berilmasa default (false).                             |
        /// | IsActive        | bool  | Yo'q     | Yo'q     | Faol holati. Berilmasa default (true).                                |
        ///
        /// **Xatolik holatlari:**
        /// - Ko'rsatilgan `StationId` bo'yicha station topilmasa — xatolik qaytadi.
        /// - Permission yetarli bo'lmasa — 403 Forbidden.
        /// </remarks>
        /// <param name="request">Qurilma ro'yxatdan o'tkazish uchun ma'lumotlar.</param>
        /// <response code="200">Qurilma muvaffaqiyatli ro'yxatdan o'tkazildi.</response>
        /// <response code="400">Validatsiya xatosi (majburiy maydonlar to'ldirilmagan).</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Ko'rsatilgan StationId bo'yicha station topilmadi.</response>
        [HttpPost]
        [RequirePermission(Permissions.DeviceAdminRegister)]
        [TypeFilter(typeof(RegisterDeviceValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Register([FromBody] RegisterDeviceRequest request)
        {
            var result = await _service.RegisterAsync(request.ToDto(), User.GetUserId(), User.GetPermissions());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Barcha qurilmalar ro'yxatini olish.
        /// </summary>
        /// <remarks>
        /// Tizimdagi barcha qurilmalarni qaytaradi (soft delete qilinganlar bundan mustasno).
        ///
        /// **Permission:** `device.admin.getall`
        /// </remarks>
        /// <response code="200">Qurilmalar ro'yxati muvaffaqiyatli qaytarildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        [HttpGet]
        [RequirePermission(Permissions.DeviceAdminGetAll)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(result.Result);
        }

        /// <summary>
        /// Qurilmani ID bo'yicha olish.
        /// </summary>
        /// <remarks>
        /// Berilgan ID bo'yicha bitta qurilma ma'lumotlarini qaytaradi.
        ///
        /// **Permission:** `device.admin.getbyid`
        /// </remarks>
        /// <param name="id">Qurilma ID si.</param>
        /// <response code="200">Qurilma topildi va qaytarildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Berilgan ID bo'yicha qurilma topilmadi.</response>
        [HttpGet("{id}")]
        [RequirePermission(Permissions.DeviceAdminGetById)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _service.GetByIdAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Stansiyaga tegishli qurilmalar ro'yxati.
        /// </summary>
        /// <remarks>
        /// Berilgan stansiya ID si bo'yicha unga tegishli barcha qurilmalarni qaytaradi.
        ///
        /// **Permission:** `device.admin.getbystation`
        /// </remarks>
        /// <param name="stationId">Stansiya ID si.</param>
        /// <response code="200">Stansiyaga tegishli qurilmalar ro'yxati qaytarildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        [HttpGet("by-station/{stationId}")]
        [RequirePermission(Permissions.DeviceAdminGetByStation)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetByStation(long stationId)
        {
            var result = await _service.GetByStationAsync(stationId);
            return Ok(result.Result);
        }

        /// <summary>
        /// Qurilma ma'lumotlarini yangilash.
        /// </summary>
        /// <remarks>
        /// Faqat readonly bo'lmagan maydonlarni yangilash mumkin. SerialNumber, DeviceType, StationId o'zgartirilmaydi.
        ///
        /// **Permission:** `device.admin.update`
        ///
        /// **Yangilanishi mumkin bo'lgan maydonlar:**
        ///
        /// | Maydon          | Turi    | Tavsif              |
        /// |-----------------|---------|---------------------|
        /// | Model           | string? | Qurilma modeli.     |
        /// | FirmwareVersion | string? | Firmware versiyasi. |
        /// | IsOnline        | bool?   | Online holati.      |
        /// | IsActive        | bool?   | Faol holati.        |
        ///
        /// Faqat yuborilgan (null bo'lmagan) maydonlar yangilanadi.
        /// </remarks>
        /// <param name="id">Yangilanadigan qurilma ID si.</param>
        /// <param name="request">Yangilanadigan maydonlar.</param>
        /// <response code="200">Qurilma muvaffaqiyatli yangilandi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Berilgan ID bo'yicha qurilma topilmadi.</response>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.DeviceAdminUpdate)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateDeviceRequest request)
        {
            var result = await _service.UpdateAsync(id, request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Qurilmani o'chirish (soft delete).
        /// </summary>
        /// <remarks>
        /// Qurilmani bazadan butunlay o'chirmaydi, `IsDeleted = true` qilib belgilaydi.
        /// O'chirilgan qurilma ro'yxatlarda ko'rinmaydi.
        ///
        /// **Permission:** `device.admin.delete`
        /// </remarks>
        /// <param name="id">O'chiriladigan qurilma ID si.</param>
        /// <response code="200">Qurilma muvaffaqiyatli o'chirildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Berilgan ID bo'yicha qurilma topilmadi.</response>
        [HttpDelete("{id}")]
        [RequirePermission(Permissions.DeviceAdminDelete)]
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
