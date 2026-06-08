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
    /// Platform foydalanuvchilarini (Manage/Merchant) admin tomonidan boshqarish.
    /// </summary>
    /// <remarks>
    /// `/api/User/*` faqat **Platform** guruhini boshqaradi:
    /// - **Manage** (`type=0`) — scope cheklovi yo'q (butun platforma). Rol global bo'lishi shart (`role.MerchantId == null`).
    /// - **Merchant** (`type=1`) — `merchantId` majburiy; o'z merchantiga scoped operator. Rol shu merchantga tegishli bo'lishi shart.
    ///
    /// **Scope:** Manage → cheklovsiz; Merchant operator (UserAdmin.* ruxsati bo'lsa) faqat o'z merchantining operatorlarini boshqaradi.
    ///
    /// **Customer foydalanuvchilari bu yerda EMAS:** Natural o'zi ro'yxatdan o'tadi (AuthApi), Corporate esa `/api/CorporateUser/*` orqali.
    ///
    /// Yangi user `IsOtpVerified=true`, `IsVerified=false` holatida yaratiladi — keyin `SetPassword`.
    /// Barcha endpointlar JWT + tegishli permission talab qiladi. Xatolik: `{ "message": "..." }`.
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
        /// Yangi platform foydalanuvchi (Manage/Merchant) yaratish.
        /// </summary>
        /// <remarks>
        /// **Permission:** `UserAdmin.Create`. Faqat Manage (yoki o'z merchanti uchun Merchant operator).
        ///
        /// **Request body maydonlari:**
        ///
        /// | Maydon      | Turi   | Majburiy | Tavsif                                                              |
        /// |-------------|--------|----------|--------------------------------------------------------------------|
        /// | PhoneId     | string | **Ha**   | Foydalanuvchi qurilma identifikatori.                              |
        /// | Mail        | string | **Ha**   | Elektron pochta.                                                    |
        /// | PhoneNumber | string | **Ha**   | Telefon raqami (998XXXXXXXXX, unique).                             |
        /// | RoleId      | long   | **Ha**   | Platform rol. Manage uchun global rol; Merchant uchun shu merchant roli. |
        /// | Type        | int    | **Ha**   | Subtip: `0=Manage`, `1=Merchant` (enum RAQAM sifatida).            |
        /// | MerchantId  | long   | Type=1 da| Merchant operatori biriktiriladigan merchant.                      |
        ///
        /// **Qoidalar:**
        /// - `Type=Manage` → rol global bo'lishi shart (`role.MerchantId == null`).
        /// - `Type=Merchant` → `MerchantId` majburiy, merchant `IsActive`, rol shu merchantga tegishli.
        /// - Yangi user `IsOtpVerified=true`, `IsVerified=false` — keyin `SetPassword`.
        ///
        /// **Response:** yaratilgan user ID si — shu ID bo'yicha `SetPassword`.
        /// </remarks>
        /// <param name="request">Platform foydalanuvchi yaratish ma'lumotlari.</param>
        /// <response code="200">Yaratildi — response da user ID.</response>
        /// <response code="400">Validatsiya yoki rol/merchant mosligi xatosi.</response>
        /// <response code="403">Ruxsat yoki scope yetarli emas.</response>
        /// <response code="404">Rol yoki merchant topilmadi.</response>
        [HttpPost]
        [RequirePermission(Permissions.UserAdminCreate)]
        [TypeFilter(typeof(CreateUserValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
        {
            var result = await _service.CreateAsync(request.ToDto(), User.GetScope());
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
            var result = await _service.GetAllAsync(param, User.GetScope());
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
            var result = await _service.GetByIdAsync(id, User.GetScope());
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
            var result = await _service.SetPasswordAsync(request.ToDto(id), User.GetScope());
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
            var result = await _service.ResetPasswordAsync(request.ToDto(id), User.GetScope());
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
            var result = await _service.BlockAsync(id, User.GetScope());
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
            var result = await _service.UnblockAsync(id, User.GetScope());
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
            var result = await _service.DeleteAsync(id, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }
    }
}
