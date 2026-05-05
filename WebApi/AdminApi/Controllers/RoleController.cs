using AdminApi.Extensions;
using Permissions = Domain.Constants.Permissions;
using AdminApi.Filters.ValidationFilters;
using AdminApi.Models.Requests;
using CommonConfiguration.Attributes;
using Domain.Enums;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminApi.Controllers
{
    /// <summary>
    /// Rollar va ruxsatlar boshqaruvi (RBAC).
    /// </summary>
    /// <remarks>
    /// Tizimda foydalanuvchilarga rol tayinlash va rollarga ruxsat (permission) berish.
    ///
    /// **RBAC tizimi:**
    /// - Har bir foydalanuvchiga bir yoki bir nechta rol tayinlanishi mumkin.
    /// - Har bir rolga bir nechta permission (ruxsat) biriktirilishi mumkin.
    /// - Permission lar orqali foydalanuvchi qaysi endpointlarga kira olishi nazorat qilinadi.
    ///
    /// **Rol scopei (RoleType):**
    /// - **NaturalRole** — global, hech qanday entityga biriktirilmagan.
    /// - **LegalRole** — tashkilotga (Organization) biriktirilgan.
    /// - **MerchantRole** — merchantga (Station orqali) biriktirilgan.
    ///
    /// **Permission flow:**
    /// - Faqat permissionning o'zi yetarli emas — caller qaysi scopega tegishli ekanligi
    ///   va qaysi entity ustida ish qilayotgani ham tekshiriladi.
    /// - Tashkilot foydalanuvchisi faqat o'z tashkiloti rollarini, merchant
    ///   foydalanuvchisi faqat o'z merchanti rollarini boshqara oladi.
    /// - Global (NaturalUser) foydalanuvchi cross-scope ishlash uchun tegishli
    ///   permissionlarga ega bo'lishi kerak.
    ///
    /// Barcha endpointlar JWT token va tegishli permission talab qiladi.
    /// Xatolik bo'lsa response body'da `{ "message": "..." }` formatida sabab qaytariladi.
    /// </remarks>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class RoleController : ControllerBase
    {
        private readonly IRoleService _roleService;

        public RoleController(IRoleService roleService)
        {
            _roleService = roleService;
        }

        /// <summary>
        /// Yangi rol yaratish.
        /// </summary>
        /// <remarks>
        /// Yangi rolni tizimga qo'shadi. Yaratish paytida ixtiyoriy ravishda permission lar ham tayinlash mumkin.
        ///
        /// **Permission:** `role.create`
        ///
        /// **Request body maydonlari:**
        ///
        /// | Maydon         | Turi   | Majburiy | Tavsif                                                                                              |
        /// |----------------|--------|----------|-----------------------------------------------------------------------------------------------------|
        /// | Name           | string | **Ha**   | Rol nomi.                                                                                           |
        /// | Description    | string | Yo'q     | Rol tavsifi.                                                                                        |
        /// | IsActive       | bool   | Yo'q     | Faol holati. Berilmasa default (true).                                                              |
        /// | StationId      | long   | Yo'q     | Berilsa — Station merchantiga biriktirilgan MerchantRole yaratiladi.                                |
        /// | OrganizationId | long   | Yo'q     | Berilsa — Tashkilotga biriktirilgan LegalRole yaratiladi. Berilmasa va StationId bo'lsa — MerchantRole, ikkalasi ham bo'lmasa global NaturalRole. |
        /// | PermissionIds  | long[] | Yo'q     | Rolga tayinlanadigan permission ID lari.                                                            |
        ///
        /// **Muhim qoidalar:**
        /// - `OrganizationId` va `StationId` ikkalasi ham berilsa — `OrganizationId` ustuvor.
        /// - Hech biri berilmasa — global NaturalRole yaratiladi (faqat global userlar uchun).
        /// - Permissionlar rol scopeiga mos bo'lishi shart (masalan, MerchantRole ga
        ///   organization permissionlari biriktirilmaydi).
        /// </remarks>
        /// <param name="request">Rol yaratish uchun ma'lumotlar.</param>
        /// <response code="200">Rol muvaffaqiyatli yaratildi.</response>
        /// <response code="400">Validatsiya yoki scope mos kelmagan permission xatosi.</response>
        /// <response code="403">Permission yetarli emas yoki scopedan tashqarida.</response>
        /// <response code="404">Berilgan tashkilot yoki stansiya topilmadi.</response>
        [HttpPost]
        [RequirePermission(Permissions.RoleCreateRole)]
        [TypeFilter(typeof(CreateRoleValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
        {
            var result = await _roleService.CreateRoleAsync(request.ToDto(), User.GetUserId(), User.GetPermissions());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Caller scopeiga tegishli rollar ro'yxatini olish.
        /// </summary>
        /// <remarks>
        /// Faqat caller kira oladigan scopelardagi rollar qaytariladi:
        /// - Tashkilot foydalanuvchisi — o'z tashkiloti LegalRole lari.
        /// - Merchant foydalanuvchisi — o'z merchanti MerchantRole lari.
        /// - Global foydalanuvchi — NaturalRole, hamda `organization.admin.getall` /
        ///   `merchant.admin.getall` permissionlari bo'yicha mos scopelardagi rollar.
        ///
        /// **Permission:** `role.getall`
        /// </remarks>
        /// <response code="200">Rollar ro'yxati muvaffaqiyatli qaytarildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        [HttpGet]
        [RequirePermission(Permissions.RoleGetAll)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _roleService.GetRolesAsync(User.GetUserId(), User.GetPermissions());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Rolni ID bo'yicha olish.
        /// </summary>
        /// <remarks>
        /// Berilgan ID bo'yicha bitta rol ma'lumotlarini qaytaradi.
        /// Caller rolning scopega kira olmasa — 403 xatolik sabab bilan qaytariladi.
        ///
        /// **Permission:** `role.getbyid`
        /// </remarks>
        /// <param name="id">Rol ID si.</param>
        /// <response code="200">Rol topildi va qaytarildi.</response>
        /// <response code="403">Caller bu rol scopega kira olmaydi.</response>
        /// <response code="404">Berilgan ID bo'yicha rol topilmadi.</response>
        [HttpGet("{id}")]
        [RequirePermission(Permissions.RoleGetById)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _roleService.GetRoleByIdAsync(id, User.GetUserId(), User.GetPermissions());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Rol ma'lumotlarini yangilash.
        /// </summary>
        /// <remarks>
        /// Faqat name, description, isActive va permissionlar yangilanadi.
        /// Rol scopei (RoleType, OrganizationId, MerchantId) o'zgartirilmaydi.
        ///
        /// **Permission:** `role.update`
        ///
        /// **Yangilanishi mumkin bo'lgan maydonlar:**
        ///
        /// | Maydon        | Turi    | Tavsif                                                                                 |
        /// |---------------|---------|----------------------------------------------------------------------------------------|
        /// | Name          | string? | Rol nomi.                                                                              |
        /// | Description   | string? | Rol tavsifi.                                                                           |
        /// | IsActive      | bool?   | Faol holati.                                                                           |
        /// | PermissionIds | long[]? | Rolga tayinlanadigan permission ID lari (rol scopega mos bo'lishi shart).              |
        ///
        /// Faqat yuborilgan (null bo'lmagan) maydonlar yangilanadi.
        /// </remarks>
        /// <param name="id">Yangilanadigan rol ID si.</param>
        /// <param name="request">Yangilanadigan maydonlar.</param>
        /// <response code="200">Rol muvaffaqiyatli yangilandi.</response>
        /// <response code="400">Permission scopega mos kelmadi.</response>
        /// <response code="403">Caller bu rol scopega kira olmaydi.</response>
        /// <response code="404">Rol topilmadi.</response>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.RoleUpdate)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateRoleRequest request)
        {
            var result = await _roleService.UpdateRoleAsync(id, request.ToDto(), User.GetUserId(), User.GetPermissions());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Rolni o'chirish (soft delete).
        /// </summary>
        /// <remarks>
        /// Rolni bazadan butunlay o'chirmaydi, `IsDeleted = true` qilib belgilaydi.
        ///
        /// **Permission:** `role.delete`
        /// </remarks>
        /// <param name="id">O'chiriladigan rol ID si.</param>
        /// <response code="200">Rol muvaffaqiyatli o'chirildi.</response>
        /// <response code="403">Caller bu rol scopega kira olmaydi.</response>
        /// <response code="404">Rol topilmadi.</response>
        [HttpDelete("{id}")]
        [RequirePermission(Permissions.RoleDelete)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _roleService.DeleteRoleAsync(id, User.GetUserId(), User.GetPermissions());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Berilgan rolning barcha ruxsatlarini ko'rish.
        /// </summary>
        /// <remarks>
        /// Caller rolga kira oladigan bo'lsagina permissionlar qaytariladi.
        ///
        /// **Permission:** `role.getpermissions`
        /// </remarks>
        /// <param name="roleId">Rol ID si.</param>
        /// <response code="200">Rol permissionlari muvaffaqiyatli qaytarildi.</response>
        /// <response code="403">Caller bu rol scopega kira olmaydi.</response>
        /// <response code="404">Rol topilmadi.</response>
        [HttpGet("{roleId}")]
        [RequirePermission(Permissions.RoleGetPermissions)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPermissions(long roleId)
        {
            var result = await _roleService.GetRolePermissionsAsync(roleId, User.GetUserId(), User.GetPermissions());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Berilgan rol turi uchun caller biriktira oladigan permissionlar ro'yxati.
        /// </summary>
        /// <remarks>
        /// Caller scopeiga qarab — shu rol turiga biriktirish mumkin bo'lgan
        /// maximal permissionlar ro'yxati qaytariladi.
        ///
        /// **Permission:** `role.getallowedpermissions`
        ///
        /// **Qoidalar:**
        /// - Tashkilot foydalanuvchisi faqat `LegalRole` uchun so'rashi mumkin.
        /// - Merchant foydalanuvchisi faqat `MerchantRole` uchun so'rashi mumkin.
        /// - Global foydalanuvchi `NaturalRole` ni boshqarishi mumkin va
        ///   tegishli cross-scope permissionlari bo'lsa `LegalRole` / `MerchantRole`
        ///   ham qaytariladi.
        /// - Rol scopega mos kelmagan permissionlar (masalan, `LegalRole` uchun
        ///   merchant tarafdagi permissionlar) ro'yxatdan chiqarib tashlanadi.
        /// </remarks>
        /// <param name="roleType">Rol turi: NaturalRole, LegalRole yoki MerchantRole.</param>
        /// <response code="200">Permissionlar ro'yxati muvaffaqiyatli qaytarildi.</response>
        /// <response code="403">Caller bu rol turini boshqara olmaydi.</response>
        [HttpGet]
        [RequirePermission(Permissions.RoleGetAllowedPermissions)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAllowedPermissions([FromQuery] RoleType roleType)
        {
            var result = await _roleService.GetAllowedPermissionsAsync(roleType, User.GetUserId(), User.GetPermissions());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }
    }
}
