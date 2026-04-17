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
    /// </summary>
    /// <remarks>
    /// Stansiya — bir nechta qurilmalar joylashgan fizik lokatsiya. Har bir stansiya bitta merchantga tegishli.
    /// Merchant — platformada mahsulotini sotadigan tashkilot.
    ///
    /// **Ierarxiya:** Merchant → Station → Device → Product
    ///
    /// **Permission level:**
    /// - `organization.*` permissioniga ega user — boshqa merchantlarga ham stansiya qo'sha oladi.
    /// - Aks holda user faqat o'ziga tegishli merchant uchun stansiya yaratishi mumkin.
    ///
    /// Barcha endpointlar JWT token va tegishli permission talab qiladi.
    /// Xatolik bo'lsa response body'da `{ "message": "..." }` formatida sabab qaytariladi.
    /// </remarks>
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
        /// <remarks>
        /// Yangi stansiyani tizimga qo'shadi va ko'rsatilgan tashkilotga biriktiradi.
        ///
        /// **Permission:** `station.admin.create`
        ///
        /// **Permission level:** Faqat `organization.*` permissioniga ega userlar boshqa tashkilotlarga stansiya qo'sha oladi.
        /// Oddiy user faqat o'zining tashkiloti uchun stansiya yaratishi mumkin.
        ///
        /// **Request body maydonlari:**
        ///
        /// | Maydon          | Turi   | Majburiy | ReadOnly | Tavsif                                                                        |
        /// |-----------------|--------|----------|----------|-------------------------------------------------------------------------------|
        /// | Name            | string | **Ha**   | Yo'q     | Stansiya nomi. Keyinchalik o'zgartirish mumkin.                               |
        /// | Location        | string | Yo'q     | Yo'q     | Stansiya joylashuvi (ixtiyoriy).                                              |
        /// | OrganizationId  | long   | **Ha**   | Ha       | Stansiya biriktirilgan tashkilot ID si. Yaratilgandan keyin o'zgartirilmaydi. |
        ///
        /// **Xatolik holatlari:**
        /// - Ko'rsatilgan `OrganizationId` bo'yicha tashkilot topilmasa — xatolik qaytadi.
        /// - User boshqa tashkilotga stansiya qo'shmoqchi bo'lsa va `organization.*` permissioni bo'lmasa — xatolik qaytadi.
        /// </remarks>
        /// <param name="request">Stansiya yaratish uchun ma'lumotlar.</param>
        /// <response code="200">Stansiya muvaffaqiyatli yaratildi.</response>
        /// <response code="400">Validatsiya xatosi (majburiy maydonlar to'ldirilmagan).</response>
        /// <response code="403">Permission yetarli emas yoki boshqa tashkilotga ruxsat yo'q.</response>
        /// <response code="404">Ko'rsatilgan OrganizationId bo'yicha tashkilot topilmadi.</response>
        [HttpPost]
        [RequirePermission(Permissions.StationAdminCreate)]
        [TypeFilter(typeof(CreateStationValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Create([FromBody] CreateStationRequest request)
        {
            var result = await _service.CreateAsync(request.ToDto(), User.GetUserId(), User.GetPermissions());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Barcha stansiyalar ro'yxatini olish.
        /// </summary>
        /// <remarks>
        /// Tizimdagi barcha stansiyalarni qaytaradi (soft delete qilinganlar bundan mustasno).
        ///
        /// **Permission:** `station.admin.getall`
        /// </remarks>
        /// <response code="200">Stansiyalar ro'yxati muvaffaqiyatli qaytarildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        [HttpGet]
        [RequirePermission(Permissions.StationAdminGetAll)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(result.Result);
        }

        /// <summary>
        /// Stansiyani ID bo'yicha olish.
        /// </summary>
        /// <remarks>
        /// Berilgan ID bo'yicha bitta stansiya ma'lumotlarini qaytaradi.
        ///
        /// **Permission:** `station.admin.getbyid`
        /// </remarks>
        /// <param name="id">Stansiya ID si.</param>
        /// <response code="200">Stansiya topildi va qaytarildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Berilgan ID bo'yicha stansiya topilmadi.</response>
        [HttpGet("{id}")]
        [RequirePermission(Permissions.StationAdminGetById)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _service.GetByIdAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Tashkilotga tegishli stansiyalar ro'yxati.
        /// </summary>
        /// <remarks>
        /// Berilgan tashkilot ID si bo'yicha unga tegishli barcha stansiyalarni qaytaradi.
        ///
        /// **Permission:** `station.admin.getbyorganization`
        /// </remarks>
        /// <param name="organizationId">Tashkilot ID si.</param>
        /// <response code="200">Tashkilotga tegishli stansiyalar ro'yxati qaytarildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        [HttpGet("by-organization/{organizationId}")]
        [RequirePermission(Permissions.StationAdminGetByOrganization)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetByOrganization(long organizationId)
        {
            var result = await _service.GetByOrganizationAsync(organizationId);
            return Ok(result.Result);
        }

        /// <summary>
        /// Stansiya ma'lumotlarini yangilash.
        /// </summary>
        /// <remarks>
        /// Faqat readonly bo'lmagan maydonlarni yangilash mumkin. OrganizationId o'zgartirilmaydi.
        ///
        /// **Permission:** `station.admin.update`
        ///
        /// **Yangilanishi mumkin bo'lgan maydonlar:**
        ///
        /// | Maydon    | Turi    | Tavsif                    |
        /// |-----------|---------|---------------------------|
        /// | Name      | string? | Stansiya nomi.            |
        /// | Location  | string? | Stansiya joylashuvi.      |
        /// | IsActive  | bool?   | Faol holati.              |
        ///
        /// Faqat yuborilgan (null bo'lmagan) maydonlar yangilanadi.
        /// </remarks>
        /// <param name="id">Yangilanadigan stansiya ID si.</param>
        /// <param name="request">Yangilanadigan maydonlar.</param>
        /// <response code="200">Stansiya muvaffaqiyatli yangilandi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Berilgan ID bo'yicha stansiya topilmadi.</response>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.StationAdminUpdate)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateStationRequest request)
        {
            var result = await _service.UpdateAsync(id, request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Stansiyani o'chirish (soft delete).
        /// </summary>
        /// <remarks>
        /// Stansiyani bazadan butunlay o'chirmaydi, `IsDeleted = true` qilib belgilaydi.
        /// O'chirilgan stansiya ro'yxatlarda ko'rinmaydi.
        ///
        /// **Permission:** `station.admin.delete`
        /// </remarks>
        /// <param name="id">O'chiriladigan stansiya ID si.</param>
        /// <response code="200">Stansiya muvaffaqiyatli o'chirildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Berilgan ID bo'yicha stansiya topilmadi.</response>
        [HttpDelete("{id}")]
        [RequirePermission(Permissions.StationAdminDelete)]
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
