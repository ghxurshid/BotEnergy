using AdminApi.Extensions;
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
    /// Inkassatsiya — qurilmalarda yig'ilgan naqd pulni olib ketish oqimi.
    /// Inkassator ilovasining backend sirti.
    /// </summary>
    /// <remarks>
    /// **Naqd pul qayerdan keladi:** mijoz kolonka ekranida "Kartani to'ldirish" ni tanlab,
    /// naqd pul soladi va u mijozning bank kartasiga o'tkaziladi. Pul jismonan qurilma
    /// ichidagi boxda qoladi va qurilmaning naqd qoldig'iga qo'shiladi.
    ///
    /// **Oqim:**
    /// 1. `Devices` — inkassator qaysi qurilmada qancha pul borligini va u xaritada
    ///    qayerdaligini ko'radi;
    /// 2. `RequestOpen` — qurilma oldida turib boxni ochtiradi (qurilmaga MQTT buyruq ketadi);
    /// 3. qurilma boxni ochib tasdiqlaydi (`cash.box.opened`);
    /// 4. `Confirm` — sanalgan summa bilan tasdiqlaydi, qurilma qoldig'i nolga tushadi.
    ///
    /// **Scope:** merchant inkassatori faqat o'z merchantining qurilmalarini ko'radi va ochadi.
    /// Manage cheklovsiz.
    ///
    /// Qoldiq faqat 4-qadamda nolga tushadi — boxning ochilgani pulning olinganini bildirmaydi.
    /// </remarks>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class IncassationController : ControllerBase
    {
        private readonly IIncassationService _service;

        public IncassationController(IIncassationService service)
            => _service = service;

        /// <summary>
        /// Naqd qoldiq va joylashuv bilan qurilmalar ro'yxati.
        /// </summary>
        /// <remarks>
        /// Xarita uchun `latitude`/`longitude`, ro'yxat uchun `cashBalance` qaytaradi.
        /// `hasOpenCollection = true` bo'lsa qurilmada tugallanmagan inkassatsiya bor —
        /// ilova "Boxni ochish" o'rniga davom etayotgan amalni ko'rsatishi kerak.
        ///
        /// **Permission:** `Incassation.GetDevices`
        /// </remarks>
        [HttpGet]
        [RequirePermission(Permissions.IncassationGetDevices)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Devices()
        {
            var result = await _service.GetDevicesAsync(User.GetScope());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result);
        }

        /// <summary>
        /// Qurilma boxini ochishni so'raydi.
        /// </summary>
        /// <remarks>
        /// Dalolatnoma yaratiladi va qurilmaga `cash.box.open` MQTT buyrug'i yuboriladi.
        /// So'rov paytidagi server qoldig'i `expectedAmount` sifatida muzlatiladi — keyin
        /// sanalgan summa shu qiymat bilan solishtiriladi.
        ///
        /// Qurilma onlayn bo'lmasa yoki buyruq uzatilmasa **503** qaytadi.
        /// Qurilmada tugallanmagan inkassatsiya bo'lsa **409**.
        ///
        /// **Permission:** `Incassation.RequestOpen`
        /// </remarks>
        [HttpPost]
        [RequirePermission(Permissions.IncassationRequestOpen)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> RequestOpen([FromBody] RequestBoxOpenRequest request)
        {
            if (request.DeviceId <= 0)
                return BadRequest(new { message = "deviceId majburiy." });

            var result = await _service.RequestOpenAsync(User.GetScope(), request.DeviceId);
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result);
        }

        /// <summary>
        /// Olingan pulni tasdiqlaydi — qurilma qoldig'i nolga tushadi.
        /// </summary>
        /// <remarks>
        /// `countedAmount` server qoldig'idan farq qilsa amal to'xtatilmaydi: ikkala qiymat
        /// ham saqlanadi va javobdagi `difference` maydonida ko'rinadi. Farq log'ga
        /// ogohlantirish sifatida yoziladi.
        ///
        /// **Permission:** `Incassation.Confirm`
        /// </remarks>
        [HttpPost]
        [RequirePermission(Permissions.IncassationConfirm)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Confirm([FromBody] ConfirmCollectionRequest request)
        {
            if (request.CollectionId <= 0)
                return BadRequest(new { message = "collectionId majburiy." });

            var result = await _service.ConfirmAsync(
                User.GetScope(), request.CollectionId, request.CountedAmount, request.Notes);

            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result);
        }

        /// <summary>
        /// Boshlangan inkassatsiyani bekor qiladi (pul olinmadi).
        /// </summary>
        /// <remarks>
        /// Qurilma qoldig'i o'zgarmaydi. Box ochilgandan keyin ham bekor qilish mumkin —
        /// inkassator fikridan qaytgan bo'lishi mumkin.
        ///
        /// **Permission:** `Incassation.Confirm`
        /// </remarks>
        [HttpPost]
        [RequirePermission(Permissions.IncassationConfirm)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Cancel([FromBody] CancelCollectionRequest request)
        {
            if (request.CollectionId <= 0)
                return BadRequest(new { message = "collectionId majburiy." });

            var result = await _service.CancelAsync(User.GetScope(), request.CollectionId, request.Notes);
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result);
        }

        /// <summary>
        /// Inkassatsiya tarixi (audit).
        /// </summary>
        /// <remarks>
        /// `deviceId` berilsa faqat shu qurilma bo'yicha. Ro'yxat umumiy konvensiya bo'yicha:
        /// bitta ustun bo'yicha sort + barcha maydonlar bo'yicha qidiruv.
        ///
        /// **Permission:** `Incassation.GetHistory`
        /// </remarks>
        [HttpGet]
        [RequirePermission(Permissions.IncassationGetHistory)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> History([FromQuery] PaginationParams param, [FromQuery] long? deviceId = null)
        {
            var result = await _service.GetHistoryAsync(User.GetScope(), param, deviceId);
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result);
        }

        /// <summary>
        /// Naqd → karta sessiyalari (audit).
        /// </summary>
        /// <remarks>
        /// `status` bilan filtrlanadi. Asosiy foydalanish holati — `status=3` (`PayoutFailed`):
        /// pul qurilmaga tushgan, lekin mijoz kartasiga o'tmagan sessiyalar. Watcher urinishlari
        /// tugagach (`nextAttemptAt = null`) ular operator aralashuvini kutadi.
        ///
        /// Karta faqat maskalangan ko'rinishda qaytadi — token hech qachon chiqarilmaydi.
        ///
        /// **Permission:** `CashSessionAdmin.GetAll`
        /// </remarks>
        [HttpGet]
        [RequirePermission(Permissions.CashSessionAdminGetAll)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> CashSessions(
            [FromQuery] PaginationParams param,
            [FromQuery] Domain.Enums.CashSessionStatus? status = null)
        {
            var result = await _service.GetCashSessionsAsync(User.GetScope(), param, status);
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result);
        }
    }
}
