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
    /// Foydalanuvchilarni admin tomonidan boshqarish.
    /// </summary>
    /// <remarks>
    /// Barcha turdagi foydalanuvchilar (LegalUser, MerchantUser) ustida CRUD operatsiyalari.
    ///
    /// **Foydalanuvchi turlari:**
    /// - **LegalUser** — yuridik iste'molchi tashkilotiga (Organization) biriktirilgan foydalanuvchi. `OrganizationId` berilsa yaratiladi.
    /// - **MerchantUser** — merchant stansiyasiga (Station) biriktirilgan xodim. `StationId` berilsa yaratiladi.
    ///
    /// **Ikki alohida ierarxiya:**
    /// - **Organization** → LegalUsers (yuridik iste'molchilar tomoni)
    /// - **Merchant** → Station → MerchantUser (sotuvchi tomoni)
    ///
    /// **Muhim qoidalar:**
    /// - `OrganizationId` va `StationId` bir vaqtda ikkalasi ham bo'sh bo'lishi mumkin emas (kamida bittasi kerak).
    /// - Agar ikkalasi ham berilsa — `OrganizationId` ustuvor (LegalUser yaratiladi).
    /// - Yangi user `IsOtpVerified = true`, `IsVerified = false` holati bilan yaratiladi.
    /// - Response da yaratilgan user ID si qaytariladi — shu ID orqali `SetPassword` chaqiriladi.
    ///
    /// **Permission level:**
    /// - User yaratishda ham permission level hisobga olinadi (ierarxiya bo'yicha).
    /// - Yuqoriroq darajadagi permissionga ega user boshqa tashkilot/stansiyalar uchun ham user yaratishi mumkin.
    ///
    /// Barcha endpointlar JWT token va tegishli permission talab qiladi.
    /// Xatolik bo'lsa response body'da `{ "message": "..." }` formatida sabab qaytariladi.
    /// </remarks>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserAdminService _service;

        public UserController(IUserAdminService service)
            => _service = service;

        /// <summary>
        /// Yangi foydalanuvchi yaratish.
        /// </summary>
        /// <remarks>
        /// Yangi foydalanuvchini tizimga qo'shadi. Berilgan maydonlarga qarab LegalUser yoki MerchantUser yaratiladi.
        ///
        /// **Permission:** `user.admin.create`
        ///
        /// **Permission level:** User yaratishda ham permission level hisobga olinadi.
        /// Yuqoriroq darajadagi permissionga ega user boshqa tashkilot/stansiyalar uchun ham user yarata oladi.
        ///
        /// **Request body maydonlari:**
        ///
        /// | Maydon         | Turi   | Majburiy | ReadOnly | Tavsif                                                                                                               |
        /// |----------------|--------|----------|----------|----------------------------------------------------------------------------------------------------------------------|
        /// | PhoneId        | string | **Ha**   | Ha       | Foydalanuvchining telefon identifikatori. Yaratilgandan keyin o'zgartirilmaydi.                                      |
        /// | Mail           | string | **Ha**   | Yo'q     | Elektron pochta manzili.                                                                                             |
        /// | PhoneNumber    | string | **Ha**   | Yo'q     | Telefon raqami.                                                                                                      |
        /// | RoleId         | long   | **Ha**   | Yo'q     | Tayinlanadigan rol ID si.                                                                                            |
        /// | OrganizationId | long   | Yo'q     | Ha       | Tashkilot ID si. Berilsa — LegalUser yaratiladi va tashkilotga biriktiriladi. Yaratilgandan keyin o'zgartirilmaydi.  |
        /// | StationId      | long   | Yo'q     | Ha       | Stansiya ID si. Berilsa — MerchantUser yaratiladi va stansiyaga biriktiriladi. Yaratilgandan keyin o'zgartirilmaydi. |
        ///
        /// **Muhim qoidalar:**
        /// - `OrganizationId` yoki `StationId` dan kamida bittasi berilishi shart.
        /// - Agar ikkalasi ham berilsa — `OrganizationId` ustuvor, LegalUser yaratiladi.
        /// - `OrganizationId` berilsa va tashkilot mavjud bo'lsa — yangi user LegalUser sifatida yuridik iste'molchi tashkilotiga qo'shiladi.
        /// - `StationId` berilsa va station hamda uning merchanti mavjud bo'lsa — yangi user MerchantUser sifatida merchant stansiyasiga qo'shiladi.
        /// - Yangi user `IsOtpVerified = true`, `IsVerified = false` holati bilan yaratiladi.
        ///
        /// **Response:** Yaratilgan foydalanuvchining ID si qaytariladi. Shu ID bo'yicha `SetPassword` chaqirib parol o'rnatish kerak.
        ///
        /// **Xatolik holatlari:**
        /// - `OrganizationId` va `StationId` ikkalasi ham berilmasa — xatolik.
        /// - Ko'rsatilgan Organization yoki Station topilmasa — xatolik.
        /// - Permission level yetarli bo'lmasa — xatolik.
        /// </remarks>
        /// <param name="request">Foydalanuvchi yaratish uchun ma'lumotlar.</param>
        /// <response code="200">Foydalanuvchi muvaffaqiyatli yaratildi. Response da user ID qaytariladi.</response>
        /// <response code="400">Validatsiya xatosi (majburiy maydonlar to'ldirilmagan yoki OrganizationId/StationId berilmagan).</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Ko'rsatilgan tashkilot yoki stansiya topilmadi.</response>
        [HttpPost]
        [RequirePermission(Permissions.UserAdminCreate)]
        [TypeFilter(typeof(CreateUserValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
        {
            var result = await _service.CreateAsync(request.ToDto(), User.GetUserId(), User.GetPermissions());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Foydalanuvchilar ro'yxatini sahifalab olish.
        /// </summary>
        /// <remarks>
        /// Tizimdagi foydalanuvchilarni sahifalab qaytaradi (soft delete qilinganlar bundan mustasno).
        ///
        /// **Permission:** `user.admin.getall`
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
        /// <response code="200">Foydalanuvchilar ro'yxati muvaffaqiyatli qaytarildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        [HttpGet]
        [RequirePermission(Permissions.UserAdminGetAll)]
        [ProducesResponseType(typeof(PagedResult<UserAdminItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAll([FromQuery] PaginationParams param)
        {
            var result = await _service.GetAllAsync(param);
            return Ok(result.Result);
        }

        /// <summary>
        /// Foydalanuvchini ID bo'yicha olish.
        /// </summary>
        /// <remarks>
        /// Berilgan ID bo'yicha bitta foydalanuvchi ma'lumotlarini qaytaradi.
        ///
        /// **Permission:** `user.admin.getbyid`
        /// </remarks>
        /// <param name="id">Foydalanuvchi ID si.</param>
        /// <response code="200">Foydalanuvchi topildi va qaytarildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Berilgan ID bo'yicha foydalanuvchi topilmadi.</response>
        [HttpGet("{id}")]
        [RequirePermission(Permissions.UserAdminGetById)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _service.GetByIdAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Foydalanuvchiga parol o'rnatish.
        /// </summary>
        /// <remarks>
        /// Foydalanuvchi yaratilgandan keyin unga parol o'rnatish uchun ishlatiladi.
        /// `Create` endpoint idan qaytgan user ID ni shu yerda ishlatish kerak.
        ///
        /// **Permission:** `user.admin.setpassword`
        ///
        /// **Request body maydonlari:**
        ///
        /// | Maydon   | Turi   | Majburiy | Tavsif                  |
        /// |----------|--------|----------|-------------------------|
        /// | Password | string | **Ha**   | O'rnatiladigan parol.   |
        /// </remarks>
        /// <param name="id">Foydalanuvchi ID si.</param>
        /// <param name="request">Parol ma'lumotlari.</param>
        /// <response code="200">Parol muvaffaqiyatli o'rnatildi.</response>
        /// <response code="400">Validatsiya xatosi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Foydalanuvchi topilmadi.</response>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.UserAdminSetPassword)]
        [TypeFilter(typeof(SetPasswordValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetPassword(long id, [FromBody] SetPasswordRequest request)
        {
            var result = await _service.SetPasswordAsync(request.ToDto(id));
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Foydalanuvchi parolini qayta o'rnatish (reset).
        /// </summary>
        /// <remarks>
        /// Mavjud foydalanuvchining parolini yangi parolga almashtiradi.
        /// Parolni unutgan yoki admin tomonidan qayta o'rnatish kerak bo'lgan holatlarda ishlatiladi.
        ///
        /// **Permission:** `user.admin.resetpassword`
        ///
        /// **Request body maydonlari:**
        ///
        /// | Maydon       | Turi   | Majburiy | Tavsif        |
        /// |--------------|--------|----------|---------------|
        /// | NewPassword  | string | **Ha**   | Yangi parol.  |
        /// </remarks>
        /// <param name="id">Foydalanuvchi ID si.</param>
        /// <param name="request">Yangi parol ma'lumotlari.</param>
        /// <response code="200">Parol muvaffaqiyatli qayta o'rnatildi.</response>
        /// <response code="400">Validatsiya xatosi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Foydalanuvchi topilmadi.</response>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.UserAdminResetPassword)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ResetPassword(long id, [FromBody] ResetPasswordRequest request)
        {
            var result = await _service.ResetPasswordAsync(request.ToDto(id));
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Foydalanuvchini bloklash.
        /// </summary>
        /// <remarks>
        /// Foydalanuvchini bloklaydi — bloklangan foydalanuvchi tizimga kira olmaydi.
        /// `IsBlocked = true` qilib belgilanadi.
        ///
        /// **Permission:** `user.admin.block`
        /// </remarks>
        /// <param name="id">Bloklanadigan foydalanuvchi ID si.</param>
        /// <response code="200">Foydalanuvchi muvaffaqiyatli bloklandi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Foydalanuvchi topilmadi.</response>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.UserAdminBlock)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Block(long id)
        {
            var result = await _service.BlockAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Foydalanuvchini blokdan chiqarish.
        /// </summary>
        /// <remarks>
        /// Bloklangan foydalanuvchini qayta faollashtiradi.
        /// `IsBlocked = false` qilib belgilanadi.
        ///
        /// **Permission:** `user.admin.unblock`
        /// </remarks>
        /// <param name="id">Blokdan chiqariladigan foydalanuvchi ID si.</param>
        /// <response code="200">Foydalanuvchi muvaffaqiyatli blokdan chiqarildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Foydalanuvchi topilmadi.</response>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.UserAdminUnblock)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Unblock(long id)
        {
            var result = await _service.UnblockAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Foydalanuvchini o'chirish (soft delete).
        /// </summary>
        /// <remarks>
        /// Foydalanuvchini bazadan butunlay o'chirmaydi, `IsDeleted = true` qilib belgilaydi.
        /// O'chirilgan foydalanuvchi ro'yxatlarda ko'rinmaydi va tizimga kira olmaydi.
        ///
        /// **Permission:** `user.admin.delete`
        /// </remarks>
        /// <param name="id">O'chiriladigan foydalanuvchi ID si.</param>
        /// <response code="200">Foydalanuvchi muvaffaqiyatli o'chirildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Foydalanuvchi topilmadi.</response>
        [HttpDelete("{id}")]
        [RequirePermission(Permissions.UserAdminDelete)]
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
