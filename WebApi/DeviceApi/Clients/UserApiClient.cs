using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace DeviceApi.Clients
{
    public class UserApiClient : IUserApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<UserApiClient> _logger;

        public UserApiClient(HttpClient httpClient, ILogger<UserApiClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task DeviceConnectAsync(string serialNumber, string sessionToken, string productType)
        {
            var body = new
            {
                serial_number = serialNumber,
                session_token = sessionToken,
                product_type = productType
            };

            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/internal/session/connect", body);
                if (!response.IsSuccessStatusCode)
                    _logger.LogWarning("DeviceConnect bajarilmadi: {Status}, Serial: {Serial}", response.StatusCode, serialNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeviceConnect so'rovida xato. Serial: {Serial}", serialNumber);
            }
        }

        public async Task DeviceProgressAsync(string serialNumber, string sessionToken, decimal quantity, decimal totalQuantity)
        {
            var body = new
            {
                serial_number = serialNumber,
                session_token = sessionToken,
                quantity,
                total_quantity = totalQuantity
            };

            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/internal/session/progress", body);
                if (!response.IsSuccessStatusCode)
                    _logger.LogWarning("DeviceProgress bajarilmadi: {Status}, Serial: {Serial}", response.StatusCode, serialNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeviceProgress so'rovida xato. Serial: {Serial}", serialNumber);
            }
        }

        public async Task DeviceFinishAsync(string serialNumber, string sessionToken, decimal finalQuantity)
        {
            var body = new
            {
                serial_number = serialNumber,
                session_token = sessionToken,
                final_quantity = finalQuantity
            };

            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/internal/session/finish", body);
                if (!response.IsSuccessStatusCode)
                    _logger.LogWarning("DeviceFinish bajarilmadi: {Status}, Serial: {Serial}", response.StatusCode, serialNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeviceFinish so'rovida xato. Serial: {Serial}", serialNumber);
            }
        }
    }
}
