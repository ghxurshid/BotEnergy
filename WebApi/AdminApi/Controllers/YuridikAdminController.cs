using AdminApi.Filters;
using AdminApi.Models.Requests;
using AdminApi.Models.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminApi.Controllers
{
    /// <summary>
    /// Yuridik foydalanuvchi yaratish (admin tomonidan).
    /// Bu yerda LegalUserEntity yaratish logikasi keyinroq qo'shiladi.
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class YuridikAdminController : ControllerBase
    {
        [HttpPost]
        [TypeFilter(typeof(CreateYuridikAdminValidationFilter))]
        public ActionResult<CreateYuridikAdminResponse> Create([FromBody] CreateYuridikAdminRequest request)
        {
            // TODO: IUserAdminService.CreateLegalUserAsync implementatsiyasi qo'shiladi
            return Ok(new CreateYuridikAdminResponse { Created = true });
        }
    }
}
