using AdminApi.Extensions;
using AdminApi.Filters;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminApi.Controllers
{
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

        [HttpPost]
        [TypeFilter(typeof(CreateRoleValidationFilter))]
        public async Task<IActionResult> CreateRole([FromBody] AdminApi.Models.Requests.CreateRoleRequest request)
        {
            var result = await _roleService.CreateRoleAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _roleService.GetRolesAsync();
            return Ok(result.Result);
        }

        [HttpPost]
        [TypeFilter(typeof(AddPermissionValidationFilter))]
        public async Task<IActionResult> AddPermission([FromBody] AdminApi.Models.Requests.AddPermissionRequest request)
        {
            var result = await _roleService.AddPermissionToRoleAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result);
        }

        [HttpDelete]
        [TypeFilter(typeof(RemovePermissionValidationFilter))]
        public async Task<IActionResult> RemovePermission([FromBody] AdminApi.Models.Requests.RemovePermissionRequest request)
        {
            var result = await _roleService.RemovePermissionFromRoleAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result);
        }

        [HttpPost]
        [TypeFilter(typeof(AssignRoleValidationFilter))]
        public async Task<IActionResult> AssignToUser([FromBody] AdminApi.Models.Requests.AssignRoleRequest request)
        {
            var result = await _roleService.AssignRoleToUserAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result);
        }

        [HttpGet("{roleId}")]
        public async Task<IActionResult> GetPermissions(long roleId)
        {
            var result = await _roleService.GetRolePermissionsAsync(roleId);
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result);
        }
    }
}
