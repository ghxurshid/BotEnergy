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
    /// Tashkilotlar (Organization) boshqaruvi — yuridik iste'molchilar tashkiloti.
    /// </summary>
    /// <remarks>
    /// Organization — yuridik iste'molchining tashkiloti. Tashkilotga yuridik foydalanuvchilar (LegalUser) biriktiriladi.
    ///
    /// **Ierarxiya:** Organization → LegalUsers
    ///
    /// Organization va Merchant — ikki alohida tushuncha:
    /// - **Organization** — platformadagi xizmatlardan foydalanadigan yuridik iste'molchi tashkiloti.
    /// - **Merchant** — platformada mahsulotini sotadigan tashkilot (Merchant → Station → Device → Product).
    ///
    /// Barcha endpointlar JWT token va tegishli permission talab qiladi.
    /// Xatolik bo'lsa response body'da `{ "message": "..." }` formatida sabab qaytariladi.
    /// </remarks>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class OrganizationController : ControllerBase
    {
        private readonly IOrganizationService _service;

        public OrganizationController(IOrganizationService service)
            => _service = service;

        /// <summary>
        /// Yangi tashkilot yaratish.
        /// </summary>
        /// <remarks>
        /// Yangi tashkilotni tizimga qo'shadi.
        ///
        /// **Permission:** `organization.admin.create`
        ///
        /// **Request body maydonlari:**
        ///
        /// | Maydon       | Turi    | Majburiy | ReadOnly | Tavsif                                                                 |
        /// |--------------|---------|----------|----------|------------------------------------------------------------------------|
        /// | Name         | string  | **Ha**   | Ha       | Tashkilot nomi. Yaratilgandan keyin o'zgartirilmaydi.                  |
        /// | Inn          | string  | **Ha**   | Ha       | INN (soliq to'lovchi raqami). Yaratilgandan keyin o'zgartirilmaydi.    |
        /// | Address      | string  | **Ha**   | Yo'q     | Tashkilot manzili. Keyinchalik o'zgartirish mumkin.                    |
        /// | PhoneNumber  | string  | **Ha**   | Yo'q     | Telefon raqami. Keyinchalik o'zgartirish mumkin.                       |
        /// | Balance      | decimal | Yo'q     | Yo'q     | Boshlang'ich balans. Berilmasa default (0).                            |
        /// | IsActive     | bool    | Yo'q     | Yo'q     | Faol holati. Berilmasa default (true).                                 |
        /// </remarks>
        /// <param name="request">Tashkilot yaratish uchun ma'lumotlar.</param>
        /// <response code="200">Tashkilot muvaffaqiyatli yaratildi.</response>
        /// <response code="400">Validatsiya xatosi (majburiy maydonlar to'ldirilmagan).</response>
        /// <response code="403">Permission yetarli emas.</response>
        [HttpPost]
        [RequirePermission(Permissions.OrganizationAdminCreate)]
        [TypeFilter(typeof(CreateOrganizationValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Create([FromBody] CreateOrganizationRequest request)
        {
            var result = await _service.CreateAsync(request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Barcha tashkilotlar ro'yxatini olish.
        /// </summary>
        /// <remarks>
        /// Tizimdagi barcha tashkilotlarni qaytaradi (soft delete qilinganlar bundan mustasno).
        ///
        /// **Permission:** `organization.admin.getall`
        /// </remarks>
        /// <response code="200">Tashkilotlar ro'yxati muvaffaqiyatli qaytarildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        [HttpGet]
        [RequirePermission(Permissions.OrganizationAdminGetAll)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(result.Result);
        }

        /// <summary>
        /// Tashkilotni ID bo'yicha olish.
        /// </summary>
        /// <remarks>
        /// Berilgan ID bo'yicha bitta tashkilot ma'lumotlarini qaytaradi.
        ///
        /// **Permission:** `organization.admin.getbyid`
        /// </remarks>
        /// <param name="id">Tashkilot ID si.</param>
        /// <response code="200">Tashkilot topildi va qaytarildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Berilgan ID bo'yicha tashkilot topilmadi.</response>
        [HttpGet("{id}")]
        [RequirePermission(Permissions.OrganizationAdminGetById)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _service.GetByIdAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Tashkilot ma'lumotlarini yangilash.
        /// </summary>
        /// <remarks>
        /// Faqat readonly bo'lmagan maydonlarni yangilash mumkin. Name, Inn o'zgartirilmaydi.
        ///
        /// **Permission:** `organization.admin.update`
        ///
        /// **Yangilanishi mumkin bo'lgan maydonlar:**
        ///
        /// | Maydon      | Turi     | Tavsif                |
        /// |-------------|----------|-----------------------|
        /// | Address     | string?  | Tashkilot manzili.    |
        /// | PhoneNumber | string?  | Telefon raqami.       |
        /// | IsActive    | bool?    | Faol holati.          |
        ///
        /// Faqat yuborilgan (null bo'lmagan) maydonlar yangilanadi.
        /// </remarks>
        /// <param name="id">Yangilanadigan tashkilot ID si.</param>
        /// <param name="request">Yangilanadigan maydonlar.</param>
        /// <response code="200">Tashkilot muvaffaqiyatli yangilandi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Berilgan ID bo'yicha tashkilot topilmadi.</response>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.OrganizationAdminUpdate)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateOrganizationRequest request)
        {
            var result = await _service.UpdateAsync(id, request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Tashkilotni o'chirish (soft delete).
        /// </summary>
        /// <remarks>
        /// Tashkilotni bazadan butunlay o'chirmaydi, `IsDeleted = true` qilib belgilaydi.
        /// O'chirilgan tashkilot ro'yxatlarda ko'rinmaydi.
        ///
        /// **Permission:** `organization.admin.delete`
        /// </remarks>
        /// <param name="id">O'chiriladigan tashkilot ID si.</param>
        /// <response code="200">Tashkilot muvaffaqiyatli o'chirildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Berilgan ID bo'yicha tashkilot topilmadi.</response>
        [HttpDelete("{id}")]
        [RequirePermission(Permissions.OrganizationAdminDelete)]
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
