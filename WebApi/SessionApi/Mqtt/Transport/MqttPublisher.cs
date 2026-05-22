using Domain.Interfaces;
using Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQTTnet.Protocol;
using SessionApi.Mqtt.Abstractions;
using SessionApi.Mqtt.Topics;

namespace SessionApi.Mqtt.Transport
{
    /// <summary>
    /// <see cref="IMqttPublisher"/> ning transport implementatsiyasi.
    /// Device secret bilan envelope+HMAC o'rab <see cref="MqttConnection"/> orqali publish qiladi.
    /// </summary>
    public sealed class MqttPublisher : IMqttPublisher
    {
        private readonly MqttConnection _connection;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MqttPublisher> _logger;

        public MqttPublisher(
            MqttConnection connection,
            IServiceScopeFactory scopeFactory,
            ILogger<MqttPublisher> logger)
        {
            _connection = connection;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task PublishRequestAsync(string serialNumber, string type, object payload, CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var deviceRepo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
            var idStore = scope.ServiceProvider.GetRequiredService<IMqttMessageIdStore>();

            var device = await deviceRepo.GetBySerialNumberAsync(serialNumber);
            if (device is null)
            {
                _logger.LogWarning("MQTT request publish rad etildi — qurilma topilmadi serial={Serial}", serialNumber);
                return;
            }

            var id = await idStore.NextOutboundIdAsync(serialNumber);
            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var json = MqttEnvelopeSerializer.Wrap(id, type, ts, payload, device.SecretKey);

            await _connection.PublishAsync(
                MqttTopics.ServerRequest(serialNumber),
                json,
                MqttQualityOfServiceLevel.AtLeastOnce,
                ct);

            _logger.LogInformation(
                "[MQTT-OUT] request serial={Serial} id={Id} type={Type}",
                serialNumber, id, type);
        }

        public async Task PublishResponseAsync(string serialNumber, long correlationId, string type, object response, CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var deviceRepo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();

            var device = await deviceRepo.GetBySerialNumberAsync(serialNumber);
            if (device is null)
            {
                _logger.LogWarning(
                    "MQTT response publish rad etildi — qurilma topilmadi serial={Serial}", serialNumber);
                return;
            }

            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var json = MqttEnvelopeSerializer.Wrap(correlationId, type, ts, response, device.SecretKey);

            await _connection.PublishAsync(
                MqttTopics.ServerResponse(serialNumber),
                json,
                MqttQualityOfServiceLevel.AtLeastOnce,
                ct);

            _logger.LogInformation(
                "[MQTT-OUT] response serial={Serial} echoId={Id} type={Type}",
                serialNumber, correlationId, type);
        }
    }
}
