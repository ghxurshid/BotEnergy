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
    /// Platform rollari va ruxsatlari boshqaruvi (RBAC).
    /// </summary>
    /// <remarks>
    /// `/api/Role/*` faqat **Platform** rollarini boshqaradi. (Corporate rollar — `/api/CorporateRole/*`.)
    ///
    /// **Rol turi (RoleKind):**
    /// - **PlatformManage** — global (`MerchantId == null`), barcha platform permissionlar mumkin.
    /// - **PlatformMerchant** — merchantga scoped (`MerchantId` to'ldirilgan), `ManageOnly`'dan tashqari to'plam.
    ///
    /// **Scope:** Manage barcha platform rollarni; Merchant operator faqat o'z merchanti rollarini boshqaradi
    /// (`AccessScope`). Har userda bitta `RoleId` FK (m:n yo'q). Permission biriktirish `PermissionScopes.IsAllowedFor(kind, ...)` bilan cheklanadi.
    ///
    /// Barcha endpointlar JWT + tegishli permission talab qiladi. Xatolik: `{ "message": "..." }`.
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
        /// | MerchantId     | long   | Yo'q     | null → PlatformManage (global) rol; to'ldirilsa → shu merchantning PlatformMerchant roli.          |
        /// | PermissionIds  | long[] | Yo'q     | Rolga tayinlanadigan permission ID lari.                                                            |
        ///
        /// **Muhim qoidalar:**
        /// - Manage → istalgan (MerchantId ixtiyoriy); Merchant operator → faqat o'z `MerchantId`.
        /// - Permissionlar rol kindiga mos bo'lishi shart (`PermissionScopes.IsAllowedFor`) — masalan,
        ///   PlatformMerchant rolga `ManageOnly` permissionlar biriktirilmaydi.
        /// </remarks>
        /// <param name="request">Platform rol yaratish ma'lumotlari.</param>
        /// <response code="200">Rol muvaffaqiyatli yaratildi.</response>
        /// <response code="400">Validatsiya yoki kindga mos kelmagan permission xatosi.</response>
        /// <response code="403">Ruxsat yetarli emas yoki scopedan tashqarida.</response>
        /// <response code="404">Berilgan merchant topilmadi.</response>
        [HttpPost]
        [RequirePermission(Permissions.RoleCreateRole)]
        [TypeFilter(typeof(CreateRoleValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
        {
            var result = await _roleService.CreateRoleAsync(request.ToDto(), User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Caller scopeiga tegishli rollar ro'yxatini olish.
        /// </summary>
        /// <remarks>
        /// Faqat caller kira oladigan scopelardagi platform rollar qaytariladi:
        /// - Manage — barcha platform rollar (PlatformManage + barcha PlatformMerchant).
        /// - Merchant operator — faqat o'z merchanti PlatformMerchant rollari.
        ///
        /// **Permission:** `Role.GetAll`
        /// </remarks>
        /// <response code="200">Rollar ro'yxati muvaffaqiyatli qaytarildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        [HttpGet]
        [RequirePermission(Permissions.RoleGetAll)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _roleService.GetRolesAsync(User.GetScope());
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
            var result = await _roleService.GetRoleByIdAsync(id, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Rol ma'lumotlarini yangilash.
        /// </summary>
        /// <remarks>
        /// Faqat name, description, isActive va permissionlar yangilanadi.
        /// Rol kindi va `MerchantId` o'zgartirilmaydi.
        ///
        /// **Permission:** `Role.Update`
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
            var result = await _roleService.UpdateRoleAsync(id, request.ToDto(), User.GetScope());
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
            var result = await _roleService.DeleteRoleAsync(id, User.GetScope());
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
            var result = await _roleService.GetRolePermissionsAsync(roleId, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Berilgan platform rol kindi uchun biriktirilishi mumkin bo'lgan permissionlar ro'yxati.
        /// </summary>
        /// <remarks>
        /// `kind` — `RoleKind`: `PlatformManage` yoki `PlatformMerchant` (query param; nom yoki raqam).
        /// `PermissionScopes.IsAllowedFor(kind, ...)` bo'yicha ruxsat etilgan permissionlar qaytariladi.
        ///
        /// **Permission:** `Role.GetAllowedPermissions`. PlatformManage kindini faqat Manage so'ray oladi.
        /// (Corporate rol uchun: `/api/CorporateRole/AllowedPermissions`.)
        /// </remarks>
        /// <param name="kind">Rol kindi: PlatformManage yoki PlatformMerchant.</param>
        /// <response code="200">Permissionlar ro'yxati muvaffaqiyatli qaytarildi.</response>
        /// <response code="403">Caller bu rol kindini boshqara olmaydi.</response>
        [HttpGet]
        [RequirePermission(Permissions.RoleGetAllowedPermissions)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAllowedPermissions([FromQuery] RoleKind kind)
        {
            var result = await _roleService.GetAllowedPermissionsAsync(kind, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }
    }
}
