using CommonConfiguration.Extensions;
using AdminApi.Extensions;
using Permissions = Domain.Constants.Permissions;
using AdminApi.Filters.ValidationFilters;
using AdminApi.Models.Requests;
using CommonConfiguration.Attributes;
using Domain.Dtos;
using Domain.Dtos.Base;
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
            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }

        /// <summary>
        /// Merchantlar ro'yxatini sahifalab olish.
        /// </summary>
        /// <remarks>
        /// Tizimdagi merchantlarni sahifalab qaytaradi (soft delete qilinganlar bundan mustasno).
        ///
        /// **Permission:** `merchant.admin.getall`
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
        /// <response code="200">Merchantlar ro'yxati muvaffaqiyatli qaytarildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        [HttpGet]
        [RequirePermission(Permissions.MerchantAdminGetAll)]
        [ProducesResponseType(typeof(PagedResult<MerchantItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAll([FromQuery] PaginationParams param)
        {
            var result = await _service.GetAllAsync(param, User.GetScope());
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
            var result = await _service.GetByIdAsync(id, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
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
        /// | IsActive    | bool?   | Faol holati.                |
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
        [TypeFilter(typeof(UpdateMerchantValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateMerchantRequest request)
        {
            var result = await _service.UpdateAsync(id, request.ToDto(), User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
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
            var result = await _service.DeleteAsync(id, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }

        /// <summary>
        /// [EXPERT] Merchant Payme credential'larini o'rnatish (hold invoice'lar shu kassa nomidan yaratiladi).
        /// </summary>
        /// <remarks>
        /// Kalit write-only saqlanadi — GET'da faqat masked (`••••1234`) qaytadi.
        /// **Permission:** `MerchantAdmin.SetPaymeCredentials` (Manage-only).
        /// </remarks>
        [HttpPost("{id}")]
        [RequirePermission(Permissions.MerchantAdminSetPaymeCredentials)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetPaymeCredentials(long id, [FromBody] SetPaymeCredentialsDto request)
        {
            var result = await _service.SetPaymeCredentialsAsync(id, request, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }

        /// <summary>
        /// [EXPERT] Payme Merchant API credential'larini o'rnatish (checkout id + callback kaliti).
        /// </summary>
        /// <remarks>
        /// Kassa credential'laridan (SetPaymeCredentials) ALOHIDA: Merchant usulida to'lovni
        /// Payme bizga callback qilib tasdiqlaydi, shuning uchun boshqa kalit ishlatiladi.
        /// Kalit write-only saqlanadi — GET'da faqat masked qaytadi.
        /// </remarks>
        [HttpPost("{id}")]
        [RequirePermission(Permissions.MerchantAdminSetPaymeMerchantCredentials)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetPaymeMerchantCredentials(
            long id, [FromBody] SetPaymeMerchantCredentialsDto request)
        {
            var result = await _service.SetPaymeMerchantCredentialsAsync(id, request, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }

        /// <summary>
        /// Merchant to'lov strategiyasini almashtirish — RUNTIME sozlama (deploy/restart kerak emas).
        /// </summary>
        /// <remarks>
        /// Merchant o'z sozlamasini o'zi boshqaradi (merchant-scoped operator ham chaqira oladi).
        ///
        /// - `defaultMethod` — yangi sessiyalar shu usul bilan ochiladi;
        /// - `enabledMethods` — mijoz tanlashi mumkin bo'lgan usullar (default shular ichida bo'lishi shart);
        /// - `refundUnusedFunds` — prepaid usullarda (Invoice/Merchant) ishlatilmagan mablag' qaytarilsinmi.
        ///
        /// O'zgarish KEYINGI sessiyalarga ta'sir qiladi: ochiq sessiya o'zi ochilgan usul bilan
        /// yakunlanadi (FIFO consume va hisob-kitob bir xil semantikada tugashi uchun).
        /// Credential'lari to'liq bo'lmagan usulni yoqib bo'lmaydi — 409 qaytadi.
        ///
        /// **Permission:** `MerchantAdmin.SetPaymentMethods`.
        /// </remarks>
        /// <response code="200">Saqlandi — keyingi sessiyalardan boshlab kuchga kiradi</response>
        /// <response code="400">Ro'yxat bo'sh yoki default usul ro'yxatda yo'q</response>
        /// <response code="403">Merchant doirangizdan tashqarida</response>
        /// <response code="404">Merchant topilmadi</response>
        /// <response code="409">Usul credential'lari sozlanmagan yoki merchant nofaol</response>
        [HttpPost("{id}")]
        [RequirePermission(Permissions.MerchantAdminSetPaymentMethods)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> SetPaymentMethods(long id, [FromBody] SetPaymentMethodsDto request)
        {
            var result = await _service.SetPaymentMethodsAsync(id, request, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }
    }
}
