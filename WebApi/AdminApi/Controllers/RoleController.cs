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
    /// Rol yaratishda bir vaqtda permission lar ham tayinlash mumkin (`PermissionIds` maydoni orqali).
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
        /// | Maydon        | Turi   | Majburiy | Tavsif                                                                           |
        /// |---------------|--------|----------|----------------------------------------------------------------------------------|
        /// | Name          | string | **Ha**   | Rol nomi. Keyinchalik o'zgartirish mumkin.                                       |
        /// | Description   | string | Yo'q     | Rol tavsifi (ixtiyoriy).                                                         |
        /// | IsActive      | bool   | Yo'q     | Faol holati. Berilmasa default (true).                                           |
        /// | PermissionIds | long[] | Yo'q     | Rolga tayinlanadigan permission ID lari. DB dagi Permissions jadvalidagi ID lar. |
        /// </remarks>
        /// <param name="request">Rol yaratish uchun ma'lumotlar.</param>
        /// <response code="200">Rol muvaffaqiyatli yaratildi.</response>
        /// <response code="400">Validatsiya xatosi (majburiy maydonlar to'ldirilmagan).</response>
        /// <response code="403">Permission yetarli emas.</response>
        [HttpPost]
        [RequirePermission(Permissions.RoleCreateRole)]
        [TypeFilter(typeof(CreateRoleValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
        {
            var result = await _roleService.CreateRoleAsync(request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Barcha rollar ro'yxatini olish.
        /// </summary>
        /// <remarks>
        /// Tizimdagi barcha rollarni qaytaradi (soft delete qilinganlar bundan mustasno).
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
            var result = await _roleService.GetRolesAsync();
            return Ok(result.Result);
        }

        /// <summary>
        /// Rolni ID bo'yicha olish.
        /// </summary>
        /// <remarks>
        /// Berilgan ID bo'yicha bitta rol ma'lumotlarini qaytaradi.
        ///
        /// **Permission:** `role.getbyid`
        /// </remarks>
        /// <param name="id">Rol ID si.</param>
        /// <response code="200">Rol topildi va qaytarildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Berilgan ID bo'yicha rol topilmadi.</response>
        [HttpGet("{id}")]
        [RequirePermission(Permissions.RoleGetById)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _roleService.GetRoleByIdAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Rol ma'lumotlarini yangilash.
        /// </summary>
        /// <remarks>
        /// Rolning barcha maydonlari o'zgartirilishi mumkin.
        ///
        /// **Permission:** `role.update`
        ///
        /// **Yangilanishi mumkin bo'lgan maydonlar:**
        ///
        /// | Maydon        | Turi    | Tavsif                                                                     |
        /// |---------------|---------|----------------------------------------------------------------------------|
        /// | Name          | string? | Rol nomi.                                                                  |
        /// | Description   | string? | Rol tavsifi.                                                               |
        /// | IsActive      | bool?   | Faol holati.                                                               |
        /// | PermissionIds | long[]? | Rolga tayinlanadigan permission ID lari. Berilsa — oldingi permission lar yangilanadi. |
        ///
        /// Faqat yuborilgan (null bo'lmagan) maydonlar yangilanadi.
        /// </remarks>
        /// <param name="id">Yangilanadigan rol ID si.</param>
        /// <param name="request">Yangilanadigan maydonlar.</param>
        /// <response code="200">Rol muvaffaqiyatli yangilandi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Berilgan ID bo'yicha rol topilmadi.</response>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.RoleUpdate)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateRoleRequest request)
        {
            var result = await _roleService.UpdateRoleAsync(id, request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Rolni o'chirish (soft delete).
        /// </summary>
        /// <remarks>
        /// Rolni bazadan butunlay o'chirmaydi, `IsDeleted = true` qilib belgilaydi.
        /// O'chirilgan rol ro'yxatlarda ko'rinmaydi.
        ///
        /// **Permission:** `role.delete`
        /// </remarks>
        /// <param name="id">O'chiriladigan rol ID si.</param>
        /// <response code="200">Rol muvaffaqiyatli o'chirildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Berilgan ID bo'yicha rol topilmadi.</response>
        [HttpDelete("{id}")]
        [RequirePermission(Permissions.RoleDelete)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _roleService.DeleteRoleAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Berilgan rolning barcha ruxsatlarini ko'rish.
        /// </summary>
        /// <remarks>
        /// Ko'rsatilgan rolga tayinlangan barcha permission larni qaytaradi.
        ///
        /// **Permission:** `role.getpermissions`
        /// </remarks>
        /// <param name="roleId">Rol ID si.</param>
        /// <response code="200">Rol permission lari muvaffaqiyatli qaytarildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Berilgan ID bo'yicha rol topilmadi.</response>
        [HttpGet("{roleId}")]
        [RequirePermission(Permissions.RoleGetPermissions)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPermissions(long roleId)
        {
            var result = await _roleService.GetRolePermissionsAsync(roleId);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Tizimdagi barcha ruxsat etilgan permissionlar ro'yxati.
        /// </summary>
        /// <remarks>
        /// Permissions jadvalidagi barcha permissionlarni `{ Id, Name }` ko'rinishida qaytaradi.
        /// Qaytarilgan `Id` qiymatlari rol yaratish yoki yangilashda `PermissionIds` maydonida ishlatiladi.
        ///
        /// **Permission:** `role.getallowedpermissions`
        ///
        /// Bu endpointga faqat rolga permission biriktira oladigan userlarga ruxsat berilishi kerak.
        /// </remarks>
        /// <response code="200">Permissionlar ro'yxati muvaffaqiyatli qaytarildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        [HttpGet]
        [RequirePermission(Permissions.RoleGetAllowedPermissions)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAllowedPermissions()
        {
            var result = await _roleService.GetAllowedPermissionsAsync();
            return Ok(result.Result);
        }
    }
}
