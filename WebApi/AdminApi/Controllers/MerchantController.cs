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
    /// Merchantlar (platformada mahsulot sotuvchi tashkilotlar) boshqaruvi.
    /// </summary>
    /// <remarks>
    /// Merchant — platformada o'z mahsulotini sotadigan tashkilot. Har bir merchantga stansiyalar biriktiriladi,
    /// stansiyalarga esa qurilmalar (device) va ularga mahsulotlar (product) bog'lanadi.
    ///
    /// **Ierarxiya:** Merchant → Station → Device → Product
    ///
    /// **Permission level:**
    /// - `merchant.*` permissioniga ega user — barcha merchantlar ustida operatsiya bajara oladi.
    /// - pastroq darajadagi permissionlarga ega user — faqat o'ziga tegishli merchant ustida ishlaydi.
    ///
    /// Barcha endpointlar JWT token va tegishli permission talab qiladi.
    /// Xatolik bo'lsa response body'da `{ "message": "..." }` formatida sabab qaytariladi.
    /// </remarks>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class MerchantController : ControllerBase
    {
        private readonly IMerchantService _service;

        public MerchantController(IMerchantService service)
            => _service = service;

        /// <summary>
        /// Yangi merchant ro'yxatdan o'tkazish.
        /// </summary>
        /// <remarks>
        /// Yangi merchant kompaniyani tizimga qo'shadi.
        ///
        /// **Permission:** `merchant.admin.register`
        ///
        /// **Request body maydonlari:**
        ///
        /// | Maydon       | Turi   | Majburiy | ReadOnly | Tavsif                                                                 |
        /// |--------------|--------|----------|----------|------------------------------------------------------------------------|
        /// | PhoneNumber  | string | **Ha**   | Yo'q     | Merchant telefon raqami. Keyinchalik o'zgartirish mumkin.              |
        /// | Inn          | string | **Ha**   | Ha       | INN (soliq to'lovchi raqami). Yaratilgandan keyin o'zgartirilmaydi.    |
        /// | BankAccount  | string | **Ha**   | Ha       | Bank hisob raqami. Yaratilgandan keyin o'zgartirilmaydi.               |
        /// | CompanyName  | string | **Ha**   | Ha       | Kompaniya nomi. Yaratilgandan keyin o'zgartirilmaydi.                  |
        /// | IsActive     | bool   | Yo'q     | Yo'q     | Faol holati. Berilmasa default (true).                                 |
        /// </remarks>
        /// <param name="request">Merchant ro'yxatdan o'tkazish ma'lumotlari.</param>
        /// <response code="200">Merchant muvaffaqiyatli ro'yxatdan o'tkazildi.</response>
        /// <response code="400">Validatsiya xatosi (majburiy maydonlar to'ldirilmagan).</response>
        /// <response code="403">Permission yetarli emas.</response>
        [HttpPost]
        [RequirePermission(Permissions.MerchantAdminRegister)]
        [TypeFilter(typeof(RegisterMerchantValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Register([FromBody] RegisterMerchantRequest request)
        {
            var result = await _service.CreateAsync(request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Barcha merchantlar ro'yxatini olish.
        /// </summary>
        /// <remarks>
        /// Tizimdagi barcha merchantlarni qaytaradi (soft delete qilinganlar bundan mustasno).
        ///
        /// **Permission:** `merchant.admin.getall`
        /// </remarks>
        /// <response code="200">Merchantlar ro'yxati muvaffaqiyatli qaytarildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        [HttpGet]
        [RequirePermission(Permissions.MerchantAdminGetAll)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(result.Result);
        }

        /// <summary>
        /// Merchantni ID bo'yicha olish.
        /// </summary>
        /// <remarks>
        /// Berilgan ID bo'yicha bitta merchant ma'lumotlarini qaytaradi.
        ///
        /// **Permission:** `merchant.admin.getbyid`
        /// </remarks>
        /// <param name="id">Merchant ID si.</param>
        /// <response code="200">Merchant topildi va qaytarildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Berilgan ID bo'yicha merchant topilmadi.</response>
        [HttpGet("{id}")]
        [RequirePermission(Permissions.MerchantAdminGetById)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _service.GetByIdAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Merchant ma'lumotlarini yangilash.
        /// </summary>
        /// <remarks>
        /// Faqat readonly bo'lmagan maydonlarni yangilash mumkin. Inn, BankAccount, CompanyName o'zgartirilmaydi.
        ///
        /// **Permission:** `merchant.admin.update`
        ///
        /// **Yangilanishi mumkin bo'lgan maydonlar:**
        ///
        /// | Maydon      | Turi    | Tavsif                      |
        /// |-------------|---------|-----------------------------|
        /// | PhoneNumber | string? | Merchant telefon raqami.    |
        ///
        /// Faqat yuborilgan (null bo'lmagan) maydonlar yangilanadi.
        /// </remarks>
        /// <param name="id">Yangilanadigan merchant ID si.</param>
        /// <param name="request">Yangilanadigan maydonlar.</param>
        /// <response code="200">Merchant muvaffaqiyatli yangilandi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Berilgan ID bo'yicha merchant topilmadi.</response>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.MerchantAdminUpdate)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateMerchantRequest request)
        {
            var result = await _service.UpdateAsync(id, request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Merchantni o'chirish (soft delete).
        /// </summary>
        /// <remarks>
        /// Merchantni bazadan butunlay o'chirmaydi, `IsDeleted = true` qilib belgilaydi.
        /// O'chirilgan merchant ro'yxatlarda ko'rinmaydi.
        ///
        /// **Permission:** `merchant.admin.delete`
        /// </remarks>
        /// <param name="id">O'chiriladigan merchant ID si.</param>
        /// <response code="200">Merchant muvaffaqiyatli o'chirildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Berilgan ID bo'yicha merchant topilmadi.</response>
        [HttpDelete("{id}")]
        [RequirePermission(Permissions.MerchantAdminDelete)]
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
