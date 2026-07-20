using AdminApi.Extensions;
using AdminApi.Filters.ValidationFilters;
using AdminApi.Models.Requests;
using CommonConfiguration.Attributes;
using Domain.Dtos.Base;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Permissions = Domain.Constants.Permissions;

namespace AdminApi.Controllers
{
    /// <summary>
    /// Corporate (tashkilot) foydalanuvchilarini boshqarish.
    /// Manage istalgan tashkilot uchun; Corporate bosh admin faqat o'z tashkiloti uchun.
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class CorporateUserController : ControllerBase
    {
        private readonly ICustomerAdminService _service;

        public CorporateUserController(ICustomerAdminService service)
            => _service = service;

        [HttpPost]
        [RequirePermission(Permissions.CustomerAdminCreate)]
        [TypeFilter(typeof(CreateCorporateUserValidationFilter))]
        public async Task<IActionResult> Create([FromBody] CreateCorporateUserRequest request)
        {
            var result = await _service.CreateAsync(request.ToDto(), User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        [HttpGet("{organizationId}")]
        [RequirePermission(Permissions.CustomerAdminGetAll)]
        public async Task<IActionResult> GetByOrganization(long organizationId, [FromQuery] PaginationParams param)
        {
            var result = await _service.GetByOrganizationAsync(organizationId, param, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        [HttpGet("{id}")]
        [RequirePermission(Permissions.CustomerAdminGetById)]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _service.GetByIdAsync(id, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        [HttpPut("{id}")]
        [RequirePermission(Permissions.CustomerAdminSetPassword)]
        [TypeFilter(typeof(SetPasswordValidationFilter))]
        public async Task<IActionResult> SetPassword(long id, [FromBody] SetPasswordRequest request)
        {
            var result = await _service.SetPasswordAsync(request.ToDto(id), User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        [HttpPut("{id}")]
        [RequirePermission(Permissions.CustomerAdminResetPassword)]
        public async Task<IActionResult> ResetPassword(long id, [FromBody] ResetPasswordRequest request)
        {
            var result = await _service.ResetPasswordAsync(request.ToDto(id), User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        [HttpPut("{id}")]
        [RequirePermission(Permissions.CustomerAdminBlock)]
        public async Task<IActionResult> Block(long id)
        {
            var result = await _service.BlockAsync(id, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        [HttpPut("{id}")]
        [RequirePermission(Permissions.CustomerAdminUnblock)]
        public async Task<IActionResult> Unblock(long id)
        {
            var result = await _service.UnblockAsync(id, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        [HttpDelete("{id}")]
        [RequirePermission(Permissions.CustomerAdminDelete)]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _service.DeleteAsync(id, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }
    }
}
