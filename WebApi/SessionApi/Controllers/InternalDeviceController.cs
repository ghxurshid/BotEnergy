using CommonConfiguration.Attributes;
using CommonConfiguration.Filters;
using Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace SessionApi.Controllers
{
    /// <summary>
    /// Servis-servis ko'prigi: MQTT ulanishi bo'lmagan API'lar (AdminApi) shu yerdan
    /// qurilmaga buyruq yubortiradi.
    ///
    /// Nega kerak: MQTT faqat SessionApi process'ida yashaydi (arxitektura invarianti),
    /// inkassatsiya endpointlari esa AdminApi'da — u yerda platform audience va
    /// permission tizimi allaqachon bor.
    ///
    /// Ikki qatlam himoya: (1) gateway bu yo'lni tashqariga route qilmaydi va servis porti
    /// firewall bilan yopiq, (2) <see cref="InternalSecretFilter"/>.
    /// </summary>
    [ApiController]
    [Route("internal/device")]
    [SkipPermissionCheck]
    [ServiceFilter(typeof(InternalSecretFilter))]
    [ApiExplorerSettings(IgnoreApi = true)]
    public sealed class InternalDeviceController : ControllerBase
    {
        private readonly IDeviceCommandSender _commandSender;
        private readonly ILogger<InternalDeviceController> _logger;

        public InternalDeviceController(
            IDeviceCommandSender commandSender,
            ILogger<InternalDeviceController> logger)
        {
            _commandSender = commandSender;
            _logger = logger;
        }

        /// <summary>Naqd boxni ochish buyrug'ini qurilmaga MQTT orqali uzatadi.</summary>
        [HttpPost("cash-box-open")]
        public async Task<IActionResult> CashBoxOpen([FromBody] CashBoxOpenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SerialNumber) || request.CollectionId <= 0)
                return BadRequest(new { message = "serialNumber va collectionId majburiy." });

            var delivered = await _commandSender.SendCashBoxOpenAsync(
                request.SerialNumber, request.CollectionId, HttpContext.RequestAborted);

            if (!delivered)
            {
                _logger.LogError(
                    "[INTERNAL] cash.box.open uzatilmadi serial={Serial} collectionId={CollectionId}",
                    request.SerialNumber, request.CollectionId);

                return StatusCode(503, new { message = "Buyruq qurilmaga uzatilmadi." });
            }

            return Ok(new { delivered = true });
        }

        public sealed class CashBoxOpenRequest
        {
            public string SerialNumber { get; set; } = string.Empty;
            public long CollectionId { get; set; }
        }
    }
}
