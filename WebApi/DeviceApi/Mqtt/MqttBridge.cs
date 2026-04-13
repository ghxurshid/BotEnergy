using System.Text;
using System.Text.Json;
using CommonConfiguration.Messaging;
using Domain.Messaging;
using Domain.Messaging.Events;
using Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;

namespace DeviceApi.Mqtt
{
    /// <summary>
    /// MQTT bilan bevosita bog'lanish — qurilmalardan telemetry qabul qiladi,
    /// qurilmalarga buyruq yuboradi.
    /// Qurilma eventlarini RabbitMQ ga uzatadi (UserApi ga yetkazish uchun).
    /// </summary>
    public sealed class MqttBridge : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly RabbitMqPublisher _rabbitPublisher;
        private readonly MqttOptions _options;
        private readonly ILogger<MqttBridge> _logger;
        private IMqttClient? _mqttClient;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        public MqttBridge(
            IServiceScopeFactory scopeFactory,
            RabbitMqPublisher rabbitPublisher,
            IOptions<MqttOptions> options,
            ILogger<MqttBridge> logger)
        {
            _scopeFactory = scopeFactory;
            _rabbitPublisher = rabbitPublisher;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();
            _mqttClient.ApplicationMessageReceivedAsync += OnMessageAsync;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!_mqttClient.IsConnected)
                    {
                        var opts = new MqttClientOptionsBuilder()
                            .WithTcpServer(_options.BrokerHost, _options.BrokerPort)
                            .WithClientId(_options.ClientId)
                            .WithCleanSession(false);

                        if (!string.IsNullOrEmpty(_options.Username))
                            opts = opts.WithCredentials(_options.Username, _options.Password);

                        await _mqttClient.ConnectAsync(opts.Build(), stoppingToken);

                        _logger.LogInformation(
                            "MQTT brokerga ulandi: {Host}:{Port}",
                            _options.BrokerHost, _options.BrokerPort);

                        await _mqttClient.SubscribeAsync(
                            new MqttTopicFilterBuilder().WithTopic("station/+/session/connected").WithAtLeastOnceQoS().Build(), stoppingToken);
                        await _mqttClient.SubscribeAsync(
                            new MqttTopicFilterBuilder().WithTopic("station/+/telemetry").WithAtMostOnceQoS().Build(), stoppingToken);
                        await _mqttClient.SubscribeAsync(
                            new MqttTopicFilterBuilder().WithTopic("station/+/session/completed").WithAtLeastOnceQoS().Build(), stoppingToken);
                        await _mqttClient.SubscribeAsync(
                            new MqttTopicFilterBuilder().WithTopic("station/+/status").WithAtLeastOnceQoS().Build(), stoppingToken);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MQTT ulanish xatosi. 5s dan keyin qayta uriniladi.");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }

            if (_mqttClient.IsConnected)
                await _mqttClient.DisconnectAsync();

            _mqttClient.Dispose();
        }

        // ── Qurilmaga buyruq yuborish (RabbitMQ dan keladi) ───────────

        public async Task PublishStartCommandAsync(string serialNumber, long productId, decimal amount)
        {
            await PublishAsync($"station/{serialNumber}/command/start", new
            {
                product_id = productId,
                amount
            });
        }

        public async Task PublishPauseCommandAsync(string serialNumber)
        {
            await PublishAsync($"station/{serialNumber}/command/pause", new { });
        }

        public async Task PublishResumeCommandAsync(string serialNumber)
        {
            await PublishAsync($"station/{serialNumber}/command/resume", new { });
        }

        public async Task PublishStopCommandAsync(string serialNumber)
        {
            await PublishAsync($"station/{serialNumber}/command/stop", new { });
        }

        // ── MQTT xabarlarni qabul qilish → RabbitMQ ga uzatish ───────

        private async Task OnMessageAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            var topic = args.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);

            var parts = topic.Split('/');
            if (parts.Length < 3 || parts[0] != "station")
                return;

            var serialNumber = parts[1];

            try
            {
                var action = string.Join("/", parts[2..]);

                // status topicda device_token talab qilinmaydi
                if (action != "status")
                {
                    var basePayload = JsonSerializer.Deserialize<DeviceAuthPayload>(payload, JsonOpts);
                    if (string.IsNullOrEmpty(basePayload?.DeviceToken))
                    {
                        _logger.LogWarning("MQTT xabar rad etildi — device_token yo'q. Topic: {Topic}", topic);
                        return;
                    }

                    using var authScope = _scopeFactory.CreateScope();
                    var deviceRepo = authScope.ServiceProvider.GetRequiredService<IDeviceRepository>();
                    var isValid = await deviceRepo.ValidateDeviceAsync(serialNumber, basePayload.DeviceToken);
                    if (!isValid)
                    {
                        _logger.LogWarning(
                            "MQTT xabar rad etildi — noto'g'ri device_token. Serial: {Serial}, Topic: {Topic}",
                            serialNumber, topic);
                        return;
                    }
                }

                switch (action)
                {
                    case "session/connected":
                        HandleDeviceConnected(serialNumber, payload);
                        break;

                    case "telemetry":
                        HandleTelemetry(serialNumber, payload);
                        break;

                    case "session/completed":
                        HandleSessionCompleted(serialNumber, payload);
                        break;

                    case "status":
                        HandleDeviceStatus(serialNumber, payload);
                        break;

                    default:
                        _logger.LogDebug("Noma'lum MQTT topic: {Topic}", topic);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MQTT xabar qayta ishlashda xato. Topic: {Topic}", topic);
            }
        }

        private void HandleDeviceConnected(string serialNumber, string payload)
        {
            var data = JsonSerializer.Deserialize<DeviceConnectedPayload>(payload, JsonOpts);
            if (data is null) return;

            _rabbitPublisher.Publish(QueueNames.EventQueue, new DeviceEvent
            {
                EventType = DeviceEventTypes.Connected,
                SerialNumber = serialNumber,
                SessionToken = data.SessionToken
            });
        }

        private void HandleTelemetry(string serialNumber, string payload)
        {
            var data = JsonSerializer.Deserialize<TelemetryPayload>(payload, JsonOpts);
            if (data is null) return;

            _rabbitPublisher.Publish(QueueNames.EventQueue, new DeviceEvent
            {
                EventType = DeviceEventTypes.Telemetry,
                SerialNumber = serialNumber,
                SessionToken = data.SessionToken,
                Quantity = data.Quantity
            });
        }

        private void HandleSessionCompleted(string serialNumber, string payload)
        {
            var data = JsonSerializer.Deserialize<SessionCompletedPayload>(payload, JsonOpts);
            if (data is null) return;

            _rabbitPublisher.Publish(QueueNames.EventQueue, new DeviceEvent
            {
                EventType = DeviceEventTypes.Completed,
                SerialNumber = serialNumber,
                SessionToken = data.SessionToken,
                FinalQuantity = data.FinalQuantity,
                EndReason = data.EndReason
            });
        }

        private void HandleDeviceStatus(string serialNumber, string payload)
        {
            _logger.LogInformation("Device {Serial} status: {Payload}", serialNumber, payload);

            _rabbitPublisher.Publish(QueueNames.EventQueue, new DeviceEvent
            {
                EventType = DeviceEventTypes.Status,
                SerialNumber = serialNumber,
                StatusPayload = payload
            });
        }

        // ── Yordamchi ─────────────────────────────────────────────────

        private async Task PublishAsync<T>(string topic, T payload)
        {
            if (_mqttClient is null || !_mqttClient.IsConnected)
            {
                _logger.LogWarning("MQTT ulanmagan. Buyruq yuborilmadi: {Topic}", topic);
                return;
            }

            var json = JsonSerializer.Serialize(payload, JsonOpts);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(json)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag(false)
                .Build();

            await _mqttClient.PublishAsync(message);
            _logger.LogDebug("MQTT publish: {Topic} -> {Payload}", topic, json);
        }
    }

    // ── MQTT Payload modellari ────────────────────────────────────────

    internal class DeviceAuthPayload
    {
        public string? DeviceToken { get; set; }
    }

    internal sealed class DeviceConnectedPayload : DeviceAuthPayload
    {
        public string SessionToken { get; set; } = string.Empty;
    }

    internal sealed class TelemetryPayload : DeviceAuthPayload
    {
        public string SessionToken { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
    }

    internal sealed class SessionCompletedPayload : DeviceAuthPayload
    {
        public string SessionToken { get; set; } = string.Empty;
        public decimal FinalQuantity { get; set; }
        public string? EndReason { get; set; }
    }
}
