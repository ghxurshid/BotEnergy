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
        /// Rolga ruxsat (permission) qo'shish.
        /// </summary>
        /// <remarks>
        /// Mavjud rolga yangi permission qo'shadi. Agar permission allaqachon tayinlangan bo'lsa — xatolik qaytadi.
        ///
        /// **Permission:** `role.addpermission`
        ///
        /// **Request body maydonlari:**
        ///
        /// | Maydon     | Turi   | Majburiy | Tavsif                                                                    |
        /// |------------|--------|----------|---------------------------------------------------------------------------|
        /// | RoleId     | long   | **Ha**   | Rol ID si.                                                                |
        /// | Permission | string | **Ha**   | Qo'shiladigan permission nomi (masalan: `device.admin.register`).         |
        /// </remarks>
        /// <param name="request">Permission qo'shish uchun ma'lumotlar.</param>
        /// <response code="200">Permission muvaffaqiyatli qo'shildi.</response>
        /// <response code="400">Validatsiya xatosi yoki permission allaqachon mavjud.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Rol topilmadi.</response>
        [HttpPost]
        [RequirePermission(Permissions.RoleAddPermission)]
        [TypeFilter(typeof(AddPermissionValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddPermission([FromBody] AddPermissionRequest request)
        {
            var result = await _roleService.AddPermissionToRoleAsync(request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Roldan ruxsatni olib tashlash.
        /// </summary>
        /// <remarks>
        /// Roldan mavjud permission ni olib tashlaydi.
        ///
        /// **Permission:** `role.removepermission`
        ///
        /// **Request body maydonlari:**
        ///
        /// | Maydon     | Turi   | Majburiy | Tavsif                                               |
        /// |------------|--------|----------|------------------------------------------------------|
        /// | RoleId     | long   | **Ha**   | Rol ID si.                                           |
        /// | Permission | string | **Ha**   | Olib tashlanadigan permission nomi.                  |
        /// </remarks>
        /// <param name="request">Permission olib tashlash uchun ma'lumotlar.</param>
        /// <response code="200">Permission muvaffaqiyatli olib tashlandi.</response>
        /// <response code="400">Validatsiya xatosi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Rol yoki permission topilmadi.</response>
        [HttpDelete]
        [RequirePermission(Permissions.RoleRemovePermission)]
        [TypeFilter(typeof(RemovePermissionValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RemovePermission([FromBody] RemovePermissionRequest request)
        {
            var result = await _roleService.RemovePermissionFromRoleAsync(request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Foydalanuvchiga rol tayinlash.
        /// </summary>
        /// <remarks>
        /// Ko'rsatilgan foydalanuvchiga rol tayinlaydi. Foydalanuvchi telefon raqami bo'yicha aniqlanadi.
        ///
        /// **Permission:** `role.assigntouser`
        ///
        /// **Request body maydonlari:**
        ///
        /// | Maydon      | Turi   | Majburiy | Tavsif                                  |
        /// |-------------|--------|----------|-----------------------------------------|
        /// | PhoneNumber | string | **Ha**   | Foydalanuvchi telefon raqami.           |
        /// | RoleId      | long   | **Ha**   | Tayinlanadigan rol ID si.               |
        /// </remarks>
        /// <param name="request">Rol tayinlash uchun ma'lumotlar.</param>
        /// <response code="200">Rol muvaffaqiyatli tayinlandi.</response>
        /// <response code="400">Validatsiya xatosi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Foydalanuvchi yoki rol topilmadi.</response>
        [HttpPost]
        [RequirePermission(Permissions.RoleAssignToUser)]
        [TypeFilter(typeof(AssignRoleValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AssignToUser([FromBody] AssignRoleRequest request)
        {
            var result = await _roleService.AssignRoleToUserAsync(request.ToDto());
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
    }
}
