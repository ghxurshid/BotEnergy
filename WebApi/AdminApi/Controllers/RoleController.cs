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
    /// Tizimda foydalanuvchilarga rol tayinlash va rollarga ruxsat (permission) berish.
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
        [HttpPost]
        [RequirePermission(Permissions.RoleCreateRole)]
        [TypeFilter(typeof(CreateRoleValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
        {
            var result = await _roleService.CreateRoleAsync(request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Barcha rollar ro'yxati.
        /// </summary>
        [HttpGet]
        [RequirePermission(Permissions.RoleGetAll)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _roleService.GetRolesAsync();
            return Ok(result.Result);
        }

        /// <summary>
        /// Rolni ID bo'yicha olish.
        /// </summary>
        [HttpGet("{id}")]
        [RequirePermission(Permissions.RoleGetById)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _roleService.GetRoleByIdAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Rol ma'lumotlarini yangilash (Name, Description, IsActive).
        /// </summary>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.RoleUpdate)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateRoleRequest request)
        {
            var result = await _roleService.UpdateRoleAsync(id, request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Rolni o'chirish (soft delete).
        /// </summary>
        [HttpDelete("{id}")]
        [RequirePermission(Permissions.RoleDelete)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _roleService.DeleteRoleAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Rolga ruxsat (permission) qo'shish.
        /// </summary>
        [HttpPost]
        [RequirePermission(Permissions.RoleAddPermission)]
        [TypeFilter(typeof(AddPermissionValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> AddPermission([FromBody] AddPermissionRequest request)
        {
            var result = await _roleService.AddPermissionToRoleAsync(request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Roldan ruxsatni olib tashlash.
        /// </summary>
        [HttpDelete]
        [RequirePermission(Permissions.RoleRemovePermission)]
        [TypeFilter(typeof(RemovePermissionValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> RemovePermission([FromBody] RemovePermissionRequest request)
        {
            var result = await _roleService.RemovePermissionFromRoleAsync(request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Foydalanuvchiga rol tayinlash.
        /// </summary>
        [HttpPost]
        [RequirePermission(Permissions.RoleAssignToUser)]
        [TypeFilter(typeof(AssignRoleValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> AssignToUser([FromBody] AssignRoleRequest request)
        {
            var result = await _roleService.AssignRoleToUserAsync(request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Berilgan rolning barcha ruxsatlarini ko'rish.
        /// </summary>
        [HttpGet("{roleId}")]
        [RequirePermission(Permissions.RoleGetPermissions)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPermissions(long roleId)
        {
            var result = await _roleService.GetRolePermissionsAsync(roleId);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }
    }
}
