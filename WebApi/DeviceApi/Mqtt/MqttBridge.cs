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
    /// MQTT bilan bevosita bog'lanish — qurilmalardan event/telemetry/heartbeat qabul qiladi,
    /// qurilmalarga buyruq yuboradi.
    /// Qurilma eventlarini RabbitMQ ga uzatadi (UserApi ga yetkazish uchun).
    ///
    /// **Topiclar:**
    ///  Server → Device:  server/{serial}/command
    ///  Device → Server:  device/{serial}/event       — connected / stopped / error / out_of_resource
    ///                    device/{serial}/telemetry  — har 5s
    ///                    device/{serial}/response   — buyruqlarga ack
    ///                    device/{serial}/heartbeat  — har 30s, LastSeenAt yangilanishi uchun
    ///                    device/{serial}/status     — diagnostika (auth talab qilinmaydi)
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
                            new MqttTopicFilterBuilder().WithTopic("device/+/event").WithAtLeastOnceQoS().Build(), stoppingToken);
                        await _mqttClient.SubscribeAsync(
                            new MqttTopicFilterBuilder().WithTopic("device/+/telemetry").WithAtMostOnceQoS().Build(), stoppingToken);
                        await _mqttClient.SubscribeAsync(
                            new MqttTopicFilterBuilder().WithTopic("device/+/response").WithAtLeastOnceQoS().Build(), stoppingToken);
                        await _mqttClient.SubscribeAsync(
                            new MqttTopicFilterBuilder().WithTopic("device/+/heartbeat").WithAtMostOnceQoS().Build(), stoppingToken);
                        await _mqttClient.SubscribeAsync(
                            new MqttTopicFilterBuilder().WithTopic("device/+/status").WithAtLeastOnceQoS().Build(), stoppingToken);
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

        public Task PublishStartCommandAsync(string serialNumber, long processId, long productId, decimal amount)
            => PublishAsync($"server/{serialNumber}/command", new
            {
                type = "start",
                process_id = processId,
                product_id = productId,
                amount
            });

        public Task PublishPauseCommandAsync(string serialNumber, long processId)
            => PublishAsync($"server/{serialNumber}/command", new
            {
                type = "pause",
                process_id = processId
            });

        public Task PublishResumeCommandAsync(string serialNumber, long processId)
            => PublishAsync($"server/{serialNumber}/command", new
            {
                type = "resume",
                process_id = processId
            });

        public Task PublishStopCommandAsync(string serialNumber, long processId)
            => PublishAsync($"server/{serialNumber}/command", new
            {
                type = "stop",
                process_id = processId
            });

        // ── MQTT xabarlarni qabul qilish → RabbitMQ ga uzatish ───────

        private async Task OnMessageAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            var topic = args.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);

            var parts = topic.Split('/');
            if (parts.Length < 3 || parts[0] != "device")
                return;

            var serialNumber = parts[1];
            var action = parts[2];

            try
            {
                // Status va heartbeat — engil topiclar, har birida auth talab qilamiz
                // (telemetry istisno: o'ta tezkor bo'lishi uchun har payloadda token tekshiriladi).
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

                    // LastSeenAt va IsOnline ni atomic update — alohida scope da
                    await deviceRepo.TouchLastSeenAsync(serialNumber);
                }

                switch (action)
                {
                    case "event":
                        HandleDeviceEvent(serialNumber, payload);
                        break;

                    case "telemetry":
                        HandleTelemetry(serialNumber, payload);
                        break;

                    case "response":
                        HandleDeviceResponse(serialNumber, payload);
                        break;

                    case "heartbeat":
                        HandleHeartbeat(serialNumber);
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

        private void HandleDeviceEvent(string serialNumber, string payload)
        {
            var data = JsonSerializer.Deserialize<DeviceEventPayload>(payload, JsonOpts);
            if (data is null) return;

            var type = (data.Type ?? string.Empty).ToLowerInvariant();

            if (type is "connect" or "connected")
            {
                _rabbitPublisher.Publish(QueueNames.EventQueue, new DeviceEvent
                {
                    EventType = DeviceEventTypes.Connected,
                    SerialNumber = serialNumber,
                    SessionToken = data.SessionToken
                });
                return;
            }

            if (type is "stopped" or "error" or "out_of_resource" or "completed")
            {
                _rabbitPublisher.Publish(QueueNames.EventQueue, new DeviceEvent
                {
                    EventType = DeviceEventTypes.Finished,
                    SerialNumber = serialNumber,
                    SessionToken = data.SessionToken,
                    ProcessId = data.ProcessId,
                    FinalQuantity = data.FinalQuantity,
                    EndReason = type
                });
                return;
            }

            _logger.LogDebug("Noma'lum device event turi: {Type}", type);
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
                ProcessId = data.ProcessId,
                Sequence = data.Sequence,
                Quantity = data.Quantity
            });
        }

        private void HandleDeviceResponse(string serialNumber, string payload)
        {
            // Response qurilmaning command-ga ack-i. Hozircha event queue-ga finished sifatida uzatamiz —
            // agar ack stop ga bo'lsa, server bu jarayonni yopish bilan band emas (allaqachon Ended).
            // Kelajakda command tracking jadvali kerak bo'ladi.
            var data = JsonSerializer.Deserialize<DeviceResponsePayload>(payload, JsonOpts);
            if (data is null) return;

            _logger.LogInformation(
                "Device {Serial} response: process={Process} command={Cmd} status={Status}",
                serialNumber, data.ProcessId, data.Command, data.Status);
        }

        private void HandleHeartbeat(string serialNumber)
        {
            // LastSeenAt allaqachon TouchLastSeenAsync orqali yangilangan.
            _rabbitPublisher.Publish(QueueNames.EventQueue, new DeviceEvent
            {
                EventType = DeviceEventTypes.Heartbeat,
                SerialNumber = serialNumber
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

    internal sealed class DeviceEventPayload : DeviceAuthPayload
    {
        public string? Type { get; set; }
        public string? SessionToken { get; set; }
        public long? ProcessId { get; set; }
        public decimal? FinalQuantity { get; set; }
    }

    internal sealed class TelemetryPayload : DeviceAuthPayload
    {
        public string SessionToken { get; set; } = string.Empty;
        public long ProcessId { get; set; }
        public long Sequence { get; set; }
        public decimal Quantity { get; set; }
    }

    internal sealed class DeviceResponsePayload : DeviceAuthPayload
    {
        public long ProcessId { get; set; }
        public string? Command { get; set; }
        public string? Status { get; set; }
    }
}
