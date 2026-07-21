using CommonConfiguration.Attributes;
using Domain.Dtos.Base;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserApi.Extensions;
using UserApi.Filters.ValidationFilters;
using UserApi.Models.Requests;
using Permissions = Domain.Constants.Permissions;

namespace UserApi.Controllers
{
    /// <summary>
    /// Corporate (yuridik mijoz) admini o'z tashkilotidagi foydalanuvchilarni boshqaradi.
    /// Customer-audience sirt: token scope'idagi OrganizationId doirasida ishlaydi —
    /// tashkilot ID so'rovda uzatilmaydi. Xuddi shu servis (ICustomerAdminService)
    /// AdminApi'dagi Manage sirti bilan bo'lishiladi; farqi faqat audience va scope manbai.
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class OrgUserController : ControllerBase
    {
        private readonly ICustomerAdminService _service;

        public OrgUserController(ICustomerAdminService service)
            => _service = service;

        /// <summary>O'z tashkilotiga yangi foydalanuvchi qo'shish.</summary>
        [HttpPost]
        [RequirePermission(Permissions.CustomerAdminCreate)]
        [TypeFilter(typeof(CreateOrgUserValidationFilter))]
        public async Task<IActionResult> Create([FromBody] CreateOrgUserRequest request)
        {
            var scope = User.GetScope();
            var result = await _service.CreateAsync(request.ToDto(scope.OrganizationId ?? 0), scope);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>O'z tashkiloti foydalanuvchilari ro'yxati (o'zini ko'rsatmaydi).</summary>
        [HttpGet]
        [RequirePermission(Permissions.CustomerAdminGetAll)]
        public async Task<IActionResult> GetAll([FromQuery] PaginationParams param)
        {
            var scope = User.GetScope();
            var result = await _service.GetByOrganizationAsync(scope.OrganizationId ?? 0, param, scope);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>Tashkilot foydalanuvchisini ID bo'yicha olish.</summary>
        [HttpGet("{id}")]
        [RequirePermission(Permissions.CustomerAdminGetById)]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _service.GetByIdAsync(id, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>Yangi foydalanuvchiga birinchi parolni o'rnatish (o'z joriy parol tasdig'i bilan).</summary>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.CustomerAdminSetPassword)]
        public async Task<IActionResult> SetPassword(long id, [FromBody] SetOrgUserPasswordRequest request)
        {
            var result = await _service.SetPasswordAsync(request.ToDto(id), User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>Foydalanuvchi parolini tiklash (o'z joriy parol tasdig'i bilan).</summary>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.CustomerAdminResetPassword)]
        public async Task<IActionResult> ResetPassword(long id, [FromBody] ResetOrgUserPasswordRequest request)
        {
            var result = await _service.ResetPasswordAsync(request.ToDto(id), User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>Tashkilot foydalanuvchisini bloklash.</summary>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.CustomerAdminBlock)]
        public async Task<IActionResult> Block(long id)
        {
            var result = await _service.BlockAsync(id, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>Tashkilot foydalanuvchisini blokdan chiqarish.</summary>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.CustomerAdminUnblock)]
        public async Task<IActionResult> Unblock(long id)
        {
            var result = await _service.UnblockAsync(id, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>Tashkilot foydalanuvchisini o'chirish (soft delete).</summary>
        [HttpDelete("{id}")]
        [RequirePermission(Permissions.CustomerAdminDelete)]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _service.DeleteAsync(id, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }
    }
}
