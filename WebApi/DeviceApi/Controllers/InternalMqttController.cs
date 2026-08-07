using CommonConfiguration.Attributes;
using CommonConfiguration.Filters;
using Domain.Helpers;
using Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace DeviceApi.Controllers
{
    /// <summary>
    /// EMQX broker uchun autentifikatsiya va avtorizatsiya hook'lari.
    ///
    /// Broker har CONNECT'da <c>authn</c>, har PUBLISH/SUBSCRIBE'da (cache miss bo'lsa)
    /// <c>authz</c> chaqiradi. Shu bilan broker qurilmalar ro'yxatini o'zida saqlamaydi —
    /// yagona haqiqat manbai baribir PostgreSQL bo'lib qoladi: qurilma o'chirilsa yoki
    /// nofaol qilinsa, u brokerdan ham darhol tushib qoladi.
    ///
    /// Endpointlar public tarmoqda ko'rinmaydi (gateway ularni route qilmaydi) va
    /// <see cref="InternalSecretFilter"/> bilan himoyalangan.
    /// </summary>
    [ApiController]
    [Route("internal/mqtt")]
    [SkipPermissionCheck]
    [ServiceFilter(typeof(InternalSecretFilter))]
    [ApiExplorerSettings(IgnoreApi = true)]
    public sealed class InternalMqttController : ControllerBase
    {
        /// <summary>Backend servislar (SessionApi) uchun broker hisobi nomi.</summary>
        private const string BackendUsername = "botenergy-backend";

        private readonly IDeviceRepository _devices;
        private readonly IConfiguration _config;
        private readonly ILogger<InternalMqttController> _logger;

        public InternalMqttController(
            IDeviceRepository devices,
            IConfiguration config,
            ILogger<InternalMqttController> logger)
        {
            _devices = devices;
            _config = config;
            _logger = logger;
        }

        /// <summary>
        /// Broker CONNECT autentifikatsiyasi. EMQX <c>{"result": "allow"|"deny"}</c> kutadi.
        /// </summary>
        [HttpPost("authn")]
        public async Task<IActionResult> Authenticate([FromBody] MqttAuthnRequest request)
        {
            // 1) Backend servis hisobi — Mqtt:Password bilan solishtiriladi.
            if (string.Equals(request.Username, BackendUsername, StringComparison.Ordinal))
            {
                var backendPassword = _config["Mqtt:Password"];
                var ok = !string.IsNullOrWhiteSpace(backendPassword)
                         && !backendPassword.StartsWith("Env_", StringComparison.Ordinal)
                         && string.Equals(backendPassword, request.Password, StringComparison.Ordinal);

                if (!ok)
                    _logger.LogWarning("MQTT authn: backend hisobi uchun noto'g'ri parol (clientId={ClientId})", request.ClientId);

                return Ok(new { result = ok ? "allow" : "deny" });
            }

            // 2) Qurilma: username = serial number.
            if (string.IsNullOrWhiteSpace(request.Username))
                return Ok(new { result = "deny" });

            // clientId serial'ga teng bo'lishi shart — aks holda bitta qurilma
            // boshqasining clientId'si bilan ulanib, uning sessiyasini uzib yuborishi mumkin.
            if (!string.Equals(request.ClientId, request.Username, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "MQTT authn rad: clientId ({ClientId}) serial ({Serial}) ga teng emas",
                    request.ClientId, request.Username);
                return Ok(new { result = "deny" });
            }

            var device = await _devices.GetBySerialNumberAsync(request.Username);
            if (device is null || !device.IsActive)
            {
                _logger.LogWarning("MQTT authn rad: qurilma topilmadi yoki nofaol serial={Serial}", request.Username);
                return Ok(new { result = "deny" });
            }

            if (!DeviceMqttCredentials.Verify(device.SecretKey, request.Password))
            {
                _logger.LogWarning("MQTT authn rad: noto'g'ri parol serial={Serial}", request.Username);
                return Ok(new { result = "deny" });
            }

            return Ok(new { result = "allow", is_superuser = false });
        }

        /// <summary>
        /// Topic ACL. Qurilma faqat o'z topic'lari bilan ishlay oladi:
        /// publish → <c>device/{serial}/*</c>, subscribe → <c>server/{serial}/*</c>.
        /// </summary>
        [HttpPost("authz")]
        public IActionResult Authorize([FromBody] MqttAuthzRequest request)
        {
            if (string.Equals(request.Username, BackendUsername, StringComparison.Ordinal))
                return Ok(new { result = "allow" });

            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Topic))
                return Ok(new { result = "deny" });

            var serial = request.Username;
            var allowed = request.Action?.ToLowerInvariant() switch
            {
                "publish" => request.Topic.StartsWith($"device/{serial}/", StringComparison.Ordinal),
                "subscribe" => request.Topic.StartsWith($"server/{serial}/", StringComparison.Ordinal),
                _ => false
            };

            if (!allowed)
                _logger.LogWarning(
                    "MQTT authz rad: serial={Serial} action={Action} topic={Topic}",
                    serial, request.Action, request.Topic);

            return Ok(new { result = allowed ? "allow" : "deny" });
        }
    }

    public sealed class MqttAuthnRequest
    {
        public string? ClientId { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    public sealed class MqttAuthzRequest
    {
        public string? ClientId { get; set; }
        public string? Username { get; set; }
        public string? Topic { get; set; }
        public string? Action { get; set; }
    }
}
