using System.Text.Json;
using Microsoft.Extensions.Logging;
using SessionApi.Mqtt.Abstractions;

namespace SessionApi.Mqtt.Handlers
{
    /// <summary>
    /// <c>device/{serial}/event</c>, <c>type=device.status</c> — diagnostika.
    /// Payload erkin shaklda; log'ga yoziladi (monitoring/grafana uchun mavjud).
    /// </summary>
    [MqttHandler(MqttHandlerTypes.DeviceStatus, MqttTopicKind.Event)]
    public sealed class DeviceStatusHandler : MqttEventHandler<JsonElement>
    {
        private readonly ILogger<DeviceStatusHandler> _logger;

        public DeviceStatusHandler(ILogger<DeviceStatusHandler> logger) => _logger = logger;

        protected override Task HandleAsync(JsonElement payload, MqttContext context)
        {
            _logger.LogInformation(
                "[device.status] serial={Serial} payload={Payload}",
                context.SerialNumber, payload.GetRawText());
            return Task.CompletedTask;
        }
    }
}
