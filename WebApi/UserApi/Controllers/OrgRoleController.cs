using CommonConfiguration.Attributes;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserApi.Extensions;
using UserApi.Models.Requests;
using Permissions = Domain.Constants.Permissions;

namespace UserApi.Controllers
{
    /// <summary>
    /// Corporate admin o'z tashkiloti rollarini boshqaradi (Customer-audience sirt).
    /// Foydalanuvchi yaratishda RoleId shu yerdagi rollardan tanlanadi.
    /// Rolga faqat corporate uchun ruxsat etilgan permissionlar biriktiriladi
    /// (servis PermissionScopes orqali tekshiradi). OrganizationId scope'dan olinadi.
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class OrgRoleController : ControllerBase
    {
        private readonly ICustomerRoleService _service;

        public OrgRoleController(ICustomerRoleService service)
            => _service = service;

        /// <summary>O'z tashkiloti uchun yangi rol yaratish.</summary>
        [HttpPost]
        [RequirePermission(Permissions.CustomerAdminCreate)]
        public async Task<IActionResult> Create([FromBody] CreateOrgRoleRequest request)
        {
            var result = await _service.CreateRoleAsync(request.ToDto(), User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>O'z tashkiloti rollari ro'yxati.</summary>
        [HttpGet]
        [RequirePermission(Permissions.CustomerAdminGetAll)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetRolesAsync(User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>Rolni ID bo'yicha olish.</summary>
        [HttpGet("{id}")]
        [RequirePermission(Permissions.CustomerAdminGetById)]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _service.GetRoleByIdAsync(id, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>Rolni yangilash.</summary>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.CustomerAdminCreate)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateOrgRoleRequest request)
        {
            var result = await _service.UpdateRoleAsync(id, request.ToDto(), User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>Rolni o'chirish.</summary>
        [HttpDelete("{id}")]
        [RequirePermission(Permissions.CustomerAdminDelete)]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _service.DeleteRoleAsync(id, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>Rolga biriktirilgan permissionlar ro'yxati.</summary>
        [HttpGet("{id}")]
        [RequirePermission(Permissions.CustomerAdminGetById)]
        public async Task<IActionResult> GetPermissions(long id)
        {
            var result = await _service.GetRolePermissionsAsync(id, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>Corporate rolga biriktirish mumkin bo'lgan permissionlar katalogi.</summary>
        [HttpGet]
        [RequirePermission(Permissions.CustomerAdminGetAll)]
        public async Task<IActionResult> AllowedPermissions()
        {
            var result = await _service.GetAllowedPermissionsAsync();
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }
    }
}
