using System.Net.Http.Json;
using CommonConfiguration.ConfigurationExtensions;
using CommonConfiguration.Filters;
using Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CommonConfiguration.Messaging
{
    /// <summary>
    /// <see cref="IDeviceCommandSender"/> ning MQTT'siz API'lar (AdminApi) uchun
    /// implementatsiyasi: buyruq SessionApi'ning internal endpointiga localhost orqali
    /// HTTP bilan uzatiladi, u yerdan MQTT'ga chiqadi.
    ///
    /// Endpoint <see cref="InternalSecretFilter"/> bilan himoyalangan va gateway uni
    /// tashqariga route qilmaydi — chaqiruv faqat server ichida bo'ladi.
    /// </summary>
    public sealed class HttpDeviceCommandSender : IDeviceCommandSender
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<HttpDeviceCommandSender> _logger;

        public HttpDeviceCommandSender(
            HttpClient http,
            IConfiguration config,
            ILogger<HttpDeviceCommandSender> logger)
        {
            _http = http;
            _config = config;
            _logger = logger;
        }

        public async Task<bool> SendCashBoxOpenAsync(
            string serialNumber, long collectionId, CancellationToken ct = default)
        {
            // ResolveSecret internal — bir xil assembly ichida (CommonConfiguration).
            // "Env_*" placeholder sozlanmagan deb sanaladi, ya'ni jimgina noto'g'ri
            // secret bilan urinib ko'rmaymiz.
            var secret = ConfigurationAddExtensions.ResolveSecret(_config, "InternalApi:SharedSecret");
            if (string.IsNullOrWhiteSpace(secret))
            {
                _logger.LogError(
                    "[CMD] InternalApi:SharedSecret sozlanmagan — SessionApi'ga buyruq yuborib bo'lmaydi.");
                return false;
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "internal/device/cash-box-open")
                {
                    Content = JsonContent.Create(new { serialNumber, collectionId })
                };
                request.Headers.Add(InternalSecretFilter.HeaderName, secret);

                using var response = await _http.SendAsync(request, ct);

                if (response.IsSuccessStatusCode)
                    return true;

                _logger.LogError(
                    "[CMD] SessionApi cash-box-open javobi {Status} serial={Serial} collectionId={CollectionId}",
                    (int)response.StatusCode, serialNumber, collectionId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[CMD] SessionApi'ga ulanib bo'lmadi serial={Serial} collectionId={CollectionId}",
                    serialNumber, collectionId);
                return false;
            }
        }
    }
}
