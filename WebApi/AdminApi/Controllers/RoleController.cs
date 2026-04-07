using AdminApi.Extensions;
using AdminApi.Filters.PermissionFilters;
using AdminApi.Filters.ValidationFilters;
using CommonConfiguration.Attributes;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminApi.Controllers
{
    /// <summary>
    /// Rollar va ruxsatlar boshqaruvi (RBAC).
    /// Tizimda foydalanuvchilarga rol tayinlash va rollarga ruxsat (permission) berish.
    ///
    /// **Imkoniyatlar:**
    /// - Yangi rol yaratish
    /// - Barcha rollarni ko'rish
    /// - Rolga ruxsat qo'shish / olib tashlash
    /// - Foydalanuvchiga rol tayinlash
    /// - Rol ruxsatlarini ko'rish
    ///
    /// **Cheklovlar:**
    /// - JWT token talab qilinadi
    /// - Permission bilan himoyalangan — faqat tegishli ruxsatga ega admin bajarishi mumkin
    /// </summary>
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
        /// Namuna so'rov:
        ///
        ///     POST /api/Role/CreateRole
        ///     {
        ///         "name": "Operator",
        ///         "description": "Stansiya operatori",
        ///         "organizationId": 1
        ///     }
        /// </remarks>
        /// <param name="request">Rol nomi, tavsifi va tashkilot ID</param>
        /// <response code="200">Rol yaratildi</response>
        [HttpPost]
        [RequirePermission(Permissions.RoleCreateRole)]
        [TypeFilter(typeof(CreateRoleValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> CreateRole([FromBody] AdminApi.Models.Requests.CreateRoleRequest request)
        {
            var result = await _roleService.CreateRoleAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result);
        }

        /// <summary>
        /// Barcha rollar ro'yxati.
        /// </summary>
        /// <response code="200">Rollar ro'yxati</response>
        [HttpGet]
        [RequirePermission(Permissions.RoleGetAll)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _roleService.GetRolesAsync();
            return Ok(result.Result);
        }

        /// <summary>
        /// Rolga ruxsat (permission) qo'shish.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/Role/AddPermission
        ///     {
        ///         "roleId": 1,
        ///         "permission": "device.manage"
        ///     }
        ///
        /// **Permission nomlari misollari:** `user.view`, `device.manage`, `station.create`, `billing.topup`
        /// </remarks>
        /// <param name="request">Rol ID va permission nomi</param>
        /// <response code="200">Ruxsat qo'shildi</response>
        [HttpPost]
        [RequirePermission(Permissions.RoleAddPermission)]
        [TypeFilter(typeof(AddPermissionValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> AddPermission([FromBody] AdminApi.Models.Requests.AddPermissionRequest request)
        {
            var result = await _roleService.AddPermissionToRoleAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result);
        }

        /// <summary>
        /// Roldan ruxsatni olib tashlash.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     DELETE /api/Role/RemovePermission
        ///     {
        ///         "roleId": 1,
        ///         "permission": "device.manage"
        ///     }
        /// </remarks>
        /// <param name="request">Rol ID va olib tashlanadigan permission nomi</param>
        /// <response code="200">Ruxsat olib tashlandi</response>
        [HttpDelete]
        [RequirePermission(Permissions.RoleRemovePermission)]
        [TypeFilter(typeof(RemovePermissionValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> RemovePermission([FromBody] AdminApi.Models.Requests.RemovePermissionRequest request)
        {
            var result = await _roleService.RemovePermissionFromRoleAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result);
        }

        /// <summary>
        /// Foydalanuvchiga rol tayinlash.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/Role/AssignToUser
        ///     {
        ///         "phoneNumber": "998901234567",
        ///         "roleId": 1
        ///     }
        /// </remarks>
        /// <param name="request">Foydalanuvchi telefon raqami va rol ID</param>
        /// <response code="200">Rol tayinlandi</response>
        [HttpPost]
        [RequirePermission(Permissions.RoleAssignToUser)]
        [TypeFilter(typeof(AssignRoleValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> AssignToUser([FromBody] AdminApi.Models.Requests.AssignRoleRequest request)
        {
            var result = await _roleService.AssignRoleToUserAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result);
        }

        /// <summary>
        /// Berilgan rolning barcha ruxsatlarini ko'rish.
        /// </summary>
        /// <param name="roleId">Rol ID. Masalan: 1</param>
        /// <response code="200">Ruxsatlar ro'yxati</response>
        /// <response code="404">Rol topilmadi</response>
        [HttpGet("{roleId}")]
        [RequirePermission(Permissions.RoleGetPermissions)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPermissions(long roleId)
        {
            var result = await _roleService.GetRolePermissionsAsync(roleId);
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result);
        }
    }
}
