using AdminApi.Extensions;
using AdminApi.Models.Requests;
using CommonConfiguration.Attributes;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Permissions = Domain.Constants.Permissions;

namespace AdminApi.Controllers
{
    /// <summary>
    /// Corporate (tashkilot) rollarini boshqarish.
    /// Manage istalgan tashkilot rollarini; Corporate bosh admin faqat o'z tashkiloti rollarini.
    /// Gating Customer admin permissionlari orqali.
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class CorporateRoleController : ControllerBase
    {
        private readonly ICustomerRoleService _service;

        public CorporateRoleController(ICustomerRoleService service)
            => _service = service;

        [HttpPost]
        [RequirePermission(Permissions.CustomerAdminCreate)]
        public async Task<IActionResult> Create([FromBody] CreateCorporateRoleRequest request)
        {
            var result = await _service.CreateRoleAsync(request.ToDto(), User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        [HttpGet]
        [RequirePermission(Permissions.CustomerAdminGetAll)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetRolesAsync(User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        [HttpGet("{id}")]
        [RequirePermission(Permissions.CustomerAdminGetById)]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _service.GetRoleByIdAsync(id, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        [HttpPut("{id}")]
        [RequirePermission(Permissions.CustomerAdminCreate)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateRoleRequest request)
        {
            var result = await _service.UpdateRoleAsync(id, request.ToDto(), User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        [HttpDelete("{id}")]
        [RequirePermission(Permissions.CustomerAdminDelete)]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _service.DeleteRoleAsync(id, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        [HttpGet("{id}")]
        [RequirePermission(Permissions.CustomerAdminGetById)]
        public async Task<IActionResult> GetPermissions(long id)
        {
            var result = await _service.GetRolePermissionsAsync(id, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        [HttpGet]
        [RequirePermission(Permissions.CustomerAdminGetAll)]
        public async Task<IActionResult> AllowedPermissions()
        {
            var result = await _service.GetAllowedPermissionsAsync();
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }
    }
}
