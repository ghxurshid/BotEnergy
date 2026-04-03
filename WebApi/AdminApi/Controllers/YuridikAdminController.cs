using AdminApi.Filters.PermissionFilters;
using AdminApi.Filters.ValidationFilters;
using AdminApi.Models.Requests;
using AdminApi.Models.Responses;
using CommonConfiguration.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminApi.Controllers
{
    /// <summary>
    /// Yuridik foydalanuvchi (LegalUser) yaratish.
    /// Admin tomonidan tashkilotga biriktirilgan yuridik foydalanuvchi yaratiladi.
    ///
    /// **Hozirgi holat:** Implementatsiya jarayonida (TODO).
    /// Kelajakda LegalUserEntity yaratish va tashkilotga biriktirish logikasi qo'shiladi.
    ///
    /// **Yuridik foydalanuvchi xususiyatlari:**
    /// - Balans tashkilot darajasida saqlanadi (Organization.Balance)
    /// - INN bilan identifikatsiya qilinadi
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class YuridikAdminController : ControllerBase
    {
        /// <summary>
        /// Yuridik foydalanuvchi yaratish (stub).
        /// Hozircha faqat stub — haqiqiy implementatsiya keyinroq qo'shiladi.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/YuridikAdmin/Create
        ///     {
        ///         "phoneNumber": "+998901234567",
        ///         "inn": "123456789"
        ///     }
        /// </remarks>
        /// <param name="request">Telefon raqam va INN</param>
        /// <response code="200">Yaratildi (stub javob)</response>
        [HttpPost]
        [RequirePermission(Permissions.YuridikAdminCreate)]
        [TypeFilter(typeof(CreateYuridikAdminValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<CreateYuridikAdminResponse> Create([FromBody] CreateYuridikAdminRequest request)
        {
            // TODO: IUserAdminService.CreateLegalUserAsync implementatsiyasi qo'shiladi
            return Ok(new CreateYuridikAdminResponse { Created = true });
        }
    }
}
