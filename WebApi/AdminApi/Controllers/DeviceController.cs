using CommonConfiguration.Extensions;
using AdminApi.Extensions;
using Permissions = Domain.Constants.Permissions;
using AdminApi.Filters.ValidationFilters;
using AdminApi.Models.Requests;
using CommonConfiguration.Attributes;
using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminApi.Controllers
{
    /// <summary>
    /// IoT qurilmalar boshqaruvi (admin panel).
    /// Qurilmalarni ro'yxatdan o'tkazish, ko'rish, yangilash va o'chirish.
    /// </summary>
    /// <remarks>
    /// **Ierarxiya:** Merchant → Station → Device → Product
    ///
    /// Qurilma (Device) — stansiyaga biriktirilgan IoT qurilma. Har bir qurilma bitta stansiyaga tegishli.
    /// Stansiya esa merchantga tegishli. Merchant — platformada mahsulotini sotadigan tashkilot.
    ///
    /// **Permission level:**
    /// - `device.*` permissioniga ega user — faqat o'ziga tegishli merchant stansiyalaridagi qurilmalarga ruxsat.
    /// - `merchant.*` permissioniga ega user — boshqa merchantlardagi stansiyalardagi qurilmalarga ham ruxsat.
    ///
    /// Barcha endpointlar JWT token va tegishli permission talab qiladi.
    /// Xatolik bo'lsa response body'da `{ "message": "..." }` formatida sabab qaytariladi.
    /// </remarks>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class DeviceController : ControllerBase
    {
        private readonly IDeviceService _service;

        public DeviceController(IDeviceService service)
            => _service = service;

        /// <summary>
        /// Yangi qurilmani ro'yxatdan o'tkazish.
        /// </summary>
        /// <remarks>
        /// Yangi IoT qurilmani tizimga qo'shadi va ko'rsatilgan stansiyaga biriktiradi.
        ///
        /// **Permission:** `device.admin.register`
        ///
        /// **Permission level:** Agar user `device.*` permissioniga ega bo'lsa — faqat o'ziga tegishli merchant stansiyalari uchun qurilma qo'sha oladi.
        /// Agar `merchant.*` permissioni bo'lsa — boshqa merchantlar stansiyalari uchun ham qo'sha oladi.
        ///
        /// **Request body maydonlari:**
        ///
        /// | Maydon          | Turi  | Majburiy | ReadOnly | Tavsif                                                                      |
        /// |-----------------|-------|----------|----------|-----------------------------------------------------------------------------|
        /// | SerialNumber    | string| **Ha**   | Ha       | Qurilmaning seriya raqami. Yaratilgandan keyin o'zgartirilmaydi.            |
        /// | DeviceType      | enum  | **Ha**   | Ha       | Qurilma turi. Yaratilgandan keyin o'zgartirilmaydi.                         |
        /// | StationId       | long  | **Ha**   | Ha       | Qurilma biriktirilgan stansiya ID si. Yaratilgandan keyin o'zgartirilmaydi. |
        /// | Model           | string| Yo'q     | Yo'q     | Qurilma modeli (ixtiyoriy).                                                 |
        /// | FirmwareVersion | string| Yo'q     | Yo'q     | Firmware versiyasi (ixtiyoriy).                                             |
        /// | IsOnline        | bool  | Yo'q     | Yo'q     | Online holati. Berilmasa default (false).                                   |
        /// | IsActive        | bool  | Yo'q     | Yo'q     | Faol holati. Berilmasa default (true).                                      |
        ///
        /// **Xatolik holatlari:**
        /// - Ko'rsatilgan `StationId` bo'yicha station topilmasa — xatolik qaytadi.
        /// - Permission yetarli bo'lmasa — 403 Forbidden.
        /// </remarks>
        /// <param name="request">Qurilma ro'yxatdan o'tkazish uchun ma'lumotlar.</param>
        /// <response code="200">Qurilma muvaffaqiyatli ro'yxatdan o'tkazildi.</response>
        /// <response code="400">Validatsiya xatosi (majburiy maydonlar to'ldirilmagan).</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Ko'rsatilgan StationId bo'yicha station topilmadi.</response>
        [HttpPost]
        [RequirePermission(Permissions.DeviceAdminRegister)]
        [TypeFilter(typeof(RegisterDeviceValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Register([FromBody] RegisterDeviceRequest request)
        {
            var result = await _service.RegisterAsync(request.ToDto(), User.GetUserId(), User.GetPermissions());
            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }

        /// <summary>
        /// Qurilmalar ro'yxatini sahifalab olish.
        /// </summary>
        /// <remarks>
        /// Tizimdagi qurilmalarni sahifalab qaytaradi (soft delete qilinganlar bundan mustasno).
        ///
        /// **Permission:** `device.admin.getall`
        ///
        /// **Query parametrlari:**
        ///
        /// | Maydon     | Turi | Majburiy | Default | Tavsif                                                           |
        /// |------------|------|----------|---------|------------------------------------------------------------------|
        /// | PageNumber | int  | Yo'q     | 1       | Sahifa raqami (1 dan boshlanadi).                                |
        /// | PageSize   | int  | Yo'q     | 20      | Bir sahifadagi yozuvlar soni. Maksimal 100 gacha cheklanadi.     |
        ///
        /// **Response:** `items` bilan birga `pageNumber`, `pageSize`, `totalCount`, `totalPages`, `hasNext`, `hasPrevious` qaytariladi.
        /// </remarks>
        /// <param name="param">Sahifalash parametrlari.</param>
        /// <response code="200">Qurilmalar ro'yxati muvaffaqiyatli qaytarildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        [HttpGet]
        [RequirePermission(Permissions.DeviceAdminGetAll)]
        [ProducesResponseType(typeof(PagedResult<DeviceItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAll([FromQuery] PaginationParams param)
        {
            var result = await _service.GetAllAsync(param, User.GetScope());
            return Ok(result.Result);
        }

        /// <summary>
        /// Qurilmani ID bo'yicha olish.
        /// </summary>
        /// <remarks>
        /// Berilgan ID bo'yicha bitta qurilma ma'lumotlarini qaytaradi.
        ///
        /// **Permission:** `device.admin.getbyid`
        /// </remarks>
        /// <param name="id">Qurilma ID si.</param>
        /// <response code="200">Qurilma topildi va qaytarildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Berilgan ID bo'yicha qurilma topilmadi.</response>
        [HttpGet("{id}")]
        [RequirePermission(Permissions.DeviceAdminGetById)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _service.GetByIdAsync(id, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }

        /// <summary>
        /// Stansiyaga tegishli qurilmalar ro'yxati.
        /// </summary>
        /// <remarks>
        /// Berilgan stansiya ID si bo'yicha unga tegishli barcha qurilmalarni qaytaradi.
        ///
        /// **Permission:** `device.admin.getbystation`
        /// </remarks>
        /// <param name="stationId">Stansiya ID si.</param>
        /// <response code="200">Stansiyaga tegishli qurilmalar ro'yxati qaytarildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        [HttpGet("by-station/{stationId}")]
        [RequirePermission(Permissions.DeviceAdminGetByStation)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetByStation(long stationId)
        {
            var result = await _service.GetByStationAsync(stationId, User.GetScope());
            return Ok(result.Result);
        }

        /// <summary>
        /// Qurilma ma'lumotlarini yangilash.
        /// </summary>
        /// <remarks>
        /// Faqat readonly bo'lmagan maydonlarni yangilash mumkin. SerialNumber, DeviceType, StationId o'zgartirilmaydi.
        ///
        /// **Permission:** `device.admin.update`
        ///
        /// **Yangilanishi mumkin bo'lgan maydonlar:**
        ///
        /// | Maydon          | Turi    | Tavsif              |
        /// |-----------------|---------|---------------------|
        /// | Model           | string? | Qurilma modeli.     |
        /// | FirmwareVersion | string? | Firmware versiyasi. |
        /// | IsOnline        | bool?   | Online holati.      |
        /// | IsActive        | bool?   | Faol holati.        |
        ///
        /// Faqat yuborilgan (null bo'lmagan) maydonlar yangilanadi.
        /// </remarks>
        /// <param name="id">Yangilanadigan qurilma ID si.</param>
        /// <param name="request">Yangilanadigan maydonlar.</param>
        /// <response code="200">Qurilma muvaffaqiyatli yangilandi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Berilgan ID bo'yicha qurilma topilmadi.</response>
        [HttpPut("{id}")]
        [RequirePermission(Permissions.DeviceAdminUpdate)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateDeviceRequest request)
        {
            var result = await _service.UpdateAsync(id, request.ToDto(), User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }

        /// <summary>
        /// Qurilmani o'chirish (soft delete).
        /// </summary>
        /// <remarks>
        /// Qurilmani bazadan butunlay o'chirmaydi, `IsDeleted = true` qilib belgilaydi.
        /// O'chirilgan qurilma ro'yxatlarda ko'rinmaydi.
        ///
        /// **Permission:** `device.admin.delete`
        /// </remarks>
        /// <param name="id">O'chiriladigan qurilma ID si.</param>
        /// <response code="200">Qurilma muvaffaqiyatli o'chirildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Berilgan ID bo'yicha qurilma topilmadi.</response>
        [HttpDelete("{id}")]
        [RequirePermission(Permissions.DeviceAdminDelete)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _service.DeleteAsync(id, User.GetScope());
            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }

        /// <summary>
        /// [EXPERT] Qurilmaning MQTT replay-counter'larini 0'ga tushirish.
        /// </summary>
        /// <remarks>
        /// MQTT correlation id counter'lari (inbound/outbound) hech qachon avtomatik reset qilinmaydi —
        /// server restart'da ham (Redis'da doimiy saqlanadi). Qurilma tomonda ular EEPROM'da yuritiladi.
        ///
        /// Bu endpoint YAGONA istisno: qurilma EEPROM'i qayta flash qilinib counter'lar 0'dan boshlanganda,
        /// serverdagi counter'larni ham 0'lash uchun. Aks holda qurilmaning barcha xabarlari replay deb rad etiladi.
        ///
        /// **Permission:** `DeviceAdmin.ResetMqttCounters` — faqat Manage (expert) rollarga biriktiriladi.
        /// </remarks>
        /// <param name="id">Qurilma ID si.</param>
        /// <param name="idStore">MQTT counter store (DI).</param>
        /// <response code="200">Counter'lar 0'ga tushirildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Berilgan ID bo'yicha qurilma topilmadi.</response>
        [HttpPost("{id}")]
        [RequirePermission(Permissions.DeviceAdminResetMqttCounters)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ResetMqttCounters(long id, [FromServices] IMqttMessageIdStore idStore)
        {
            var device = await _service.GetByIdAsync(id, User.GetScope());
            if (!device.IsSuccess)
                return device.ToErrorResponse();

            await idStore.ResetAsync(device.Result!.SerialNumber);
            return Ok(new { message = $"MQTT counter'lar 0'ga tushirildi: {device.Result.SerialNumber}" });
        }

        /// <summary>
        /// Qurilmaning MQTT broker credential'larini qaytaradi (provisioning uchun).
        /// </summary>
        /// <remarks>
        /// Firmware'ga yoziladigan qiymatlar:
        /// - `username` va `clientId` — qurilmaning serial raqami (broker ikkalasi teng bo'lishini talab qiladi);
        /// - `password` — `SecretKey` dan bir tomonlama hosil qilingan broker paroli;
        /// - `secretKey` — envelope HMAC imzosi uchun (broker uni HECH QACHON ko'rmaydi).
        ///
        /// Parol alohida ustunda saqlanmaydi — u har safar `SecretKey` dan qayta hisoblanadi.
        /// Shu sababli broker authn hook'i parolni bilsa ham `SecretKey` ni tiklay olmaydi:
        /// HMAC qatlami mustaqil himoya bo'lib qoladi.
        ///
        /// **Permission:** `DeviceAdmin.MqttCredentials` — faqat Manage (provisioning) rollarga biriktiriladi.
        /// </remarks>
        /// <param name="id">Qurilma ID si.</param>
        /// <param name="deviceRepository">Qurilma repositoriysi (DI) — SecretKey DTO'da qaytarilmaydi.</param>
        /// <response code="200">Credential'lar qaytarildi.</response>
        /// <response code="403">Permission yetarli emas.</response>
        /// <response code="404">Berilgan ID bo'yicha qurilma topilmadi.</response>
        [HttpGet("{id}")]
        [RequirePermission(Permissions.DeviceAdminMqttCredentials)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> MqttCredentials(
            long id,
            [FromServices] Domain.Repositories.IDeviceRepository deviceRepository)
        {
            // Scope tekshiruvi servis orqali — merchant o'zgalarning qurilmasini ko'ra olmasin.
            var device = await _service.GetByIdAsync(id, User.GetScope());
            if (!device.IsSuccess)
                return device.ToErrorResponse();

            var entity = await deviceRepository.GetBySerialNumberAsync(device.Result!.SerialNumber);
            if (entity is null)
                return NotFound(new { message = "Qurilma topilmadi." });

            return Ok(new
            {
                serialNumber = entity.SerialNumber,
                clientId = entity.SerialNumber,
                username = entity.SerialNumber,
                password = Domain.Helpers.DeviceMqttCredentials.DerivePassword(entity.SecretKey),
                secretKey = entity.SecretKey,
                topics = new
                {
                    publish = $"device/{entity.SerialNumber}/{{request|response|event|telemetry|state}}",
                    subscribe = $"server/{entity.SerialNumber}/{{request|response}}"
                }
            });
        }
    }
}
