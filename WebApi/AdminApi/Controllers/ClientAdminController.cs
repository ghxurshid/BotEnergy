using AdminApi.Extensions;
using AdminApi.Filters.PermissionFilters;
using AdminApi.Filters.ValidationFilters;
using AdminApi.Models.Requests;
using CommonConfiguration.Attributes;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminApi.Controllers
{
    /// <summary>
    /// Klientlar (yuridik mijozlar) boshqaruvi.
    /// Shartnoma asosida ishlayotgan kompaniya-klientlarni ro'yxatdan o'tkazish va boshqarish.
    ///
    /// **Imkoniyatlar:**
    /// - Klient ro'yxatdan o'tkazish (INN, bank hisob raqami, kompaniya nomi)
    /// - Barcha klientlarni ko'rish
    /// - ID bo'yicha klient olish
    /// - Klient ma'lumotlarini yangilash
    /// - Klientni o'chirish
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class ClientAdminController : ControllerBase
    {
        private readonly IClientService _service;

        public ClientAdminController(IClientService service)
            => _service = service;

        /// <summary>
        /// Yangi klient ro'yxatdan o'tkazish.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/ClientAdmin/Register
        ///     {
        ///         "phoneNumber": "998901234567",
        ///         "inn": "123456789",
        ///         "bankAccount": "20208000900100001001",
        ///         "companyName": "Tech Solutions LLC"
        ///     }
        /// </remarks>
        /// <param name="request">Klient ma'lumotlari</param>
        /// <response code="200">Klient ro'yxatdan o'tkazildi</response>
        [HttpPost]
        [RequirePermission(Permissions.ClientAdminRegister)]
        [TypeFilter(typeof(RegisterClientValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Register([FromBody] RegisterClientRequest request)
        {
            var result = await _service.CreateAsync(request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Barcha klientlar ro'yxati.
        /// </summary>
        /// <response code="200">Klientlar ro'yxati</response>
        [HttpGet]
        [RequirePermission(Permissions.ClientAdminGetAll)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(result.Result);
        }

        /// <summary>
        /// Klientni ID bo'yicha olish.
        /// </summary>
        /// <param name="id">Klient ID. Masalan: 1</param>
        /// <response code="200">Klient ma'lumotlari</response>
        /// <response code="404">Klient topilmadi</response>
        [HttpGet("{id}")]
        [RequirePermission(Permissions.ClientAdminGetById)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _service.GetByIdAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Klient ma'lumotlarini yangilash.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     PUT /api/ClientAdmin/Update/1
        ///     {
        ///         "phoneNumber": "998909876543",
        ///         "companyName": "Tech Solutions Group"
        ///     }
        ///
        /// Faqat yuborilgan maydonlar yangilanadi. `null` qoldirilsa o'zgarmaydi.
        /// </remarks>
        /// <param name="id">Yangilanadigan klient ID</param>
        /// <param name="request">Yangilanadigan maydonlar</param>
        /// <response code="200">Klient yangilandi</response>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.ClientAdminUpdate)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateClientRequest request)
        {
            var result = await _service.UpdateAsync(id, request.ToDto());
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Klientni o'chirish (soft delete).
        /// </summary>
        /// <param name="id">O'chiriladigan klient ID. Masalan: 1</param>
        /// <response code="200">Klient o'chirildi</response>
        [HttpDelete("{id}")]
        [RequirePermission(Permissions.ClientAdminDelete)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _service.DeleteAsync(id);
            return result.IsSuccess ? Ok(result.Result) : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }
    }
}
