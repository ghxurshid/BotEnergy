using System.Text;
using System.Text.Json;
using Domain.Dtos.Session;
using Domain.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using UsageSessionApi.Hubs;

namespace UsageSessionApi.Mqtt
{
    /// <summary>
    /// MQTT bilan bevosita bog'lanish — qurilmalardan telemetry qabul qiladi,
    /// qurilmalarga buyruq yuboradi. UsageSession Service yuragi.
    /// </summary>
    public sealed class MqttSessionBridge : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<UsageSessionHub> _hubContext;
        private readonly MqttSessionOptions _options;
        private readonly ILogger<MqttSessionBridge> _logger;
        private IMqttClient? _mqttClient;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        public MqttSessionBridge(
            IServiceScopeFactory scopeFactory,
            IHubContext<UsageSessionHub> hubContext,
            IOptions<MqttSessionOptions> options,
            ILogger<MqttSessionBridge> logger)
        {
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
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

                        // Subscribe: qurilmalardan keluvchi barcha xabarlar
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

        // ── Qurilmaga buyruq yuborish ──────────────────────────────────

        public async Task PublishStartCommandAsync(string deviceSerialNumber, long productId, decimal amount)
        {
            await PublishAsync($"station/{deviceSerialNumber}/command/start", new
            {
                product_id = productId,
                amount
            });
        }

        public async Task PublishPauseCommandAsync(string deviceSerialNumber)
        {
            await PublishAsync($"station/{deviceSerialNumber}/command/pause", new { });
        }

        public async Task PublishResumeCommandAsync(string deviceSerialNumber)
        {
            await PublishAsync($"station/{deviceSerialNumber}/command/resume", new { });
        }

        public async Task PublishStopCommandAsync(string deviceSerialNumber)
        {
            await PublishAsync($"station/{deviceSerialNumber}/command/stop", new { });
        }

        // ── MQTT xabarlarni qabul qilish ──────────────────────────────

        private async Task OnMessageAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            var topic = args.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);

            // Topic format: station/{serialNumber}/{action} yoki station/{serialNumber}/session/{action}
            var parts = topic.Split('/');
            if (parts.Length < 3 || parts[0] != "station")
                return;

            var serialNumber = parts[1];

            try
            {
                var action = string.Join("/", parts[2..]);

                switch (action)
                {
                    case "session/connected":
                        await HandleDeviceConnectedAsync(serialNumber, payload);
                        break;

                    case "telemetry":
                        await HandleTelemetryAsync(serialNumber, payload);
                        break;

                    case "session/completed":
                        await HandleSessionCompletedAsync(serialNumber, payload);
                        break;

                    case "status":
                        await HandleDeviceStatusAsync(serialNumber, payload);
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

        /// <summary>
        /// IoT qurilma QR kodni o'qib sessiyaga ulanganda.
        /// Payload: { session_token }
        /// </summary>
        private async Task HandleDeviceConnectedAsync(string serialNumber, string payload)
        {
            var data = JsonSerializer.Deserialize<DeviceConnectedPayload>(payload, JsonOpts);
            if (data is null) return;

            using var scope = _scopeFactory.CreateScope();
            var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();

            await sessionService.DeviceConnectAsync(new DeviceConnectedDto
            {
                SessionToken = data.SessionToken,
                SerialNumber = serialNumber
            });
        }

        /// <summary>
        /// Qurilmadan har 3-5 sekundda keluvchi telemetriya.
        /// Payload: { session_token, quantity }
        /// </summary>
        private async Task HandleTelemetryAsync(string serialNumber, string payload)
        {
            var data = JsonSerializer.Deserialize<TelemetryPayload>(payload, JsonOpts);
            if (data is null) return;

            using var scope = _scopeFactory.CreateScope();
            var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();

            await sessionService.ReportProgressAsync(new SessionProgressDto
            {
                SessionToken = data.SessionToken,
                SerialNumber = serialNumber,
                Quantity = data.Quantity
            });
        }

        /// <summary>
        /// Qurilma xizmatni tugatganda (limitga yetganda yoki o'zi to'xtatganda).
        /// Payload: { session_token, final_quantity, end_reason? }
        /// </summary>
        private async Task HandleSessionCompletedAsync(string serialNumber, string payload)
        {
            var data = JsonSerializer.Deserialize<SessionCompletedPayload>(payload, JsonOpts);
            if (data is null) return;

            using var scope = _scopeFactory.CreateScope();
            var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();

            await sessionService.DeviceFinishAsync(new DeviceFinishDto
            {
                SessionToken = data.SessionToken,
                SerialNumber = serialNumber,
                FinalQuantity = data.FinalQuantity,
                EndReason = data.EndReason
            });
        }

        /// <summary>
        /// Qurilma holati o'zgarganda (online/offline/error).
        /// </summary>
        private Task HandleDeviceStatusAsync(string serialNumber, string payload)
        {
            _logger.LogInformation(
                "Device {Serial} status: {Payload}",
                serialNumber, payload);
            return Task.CompletedTask;
        }

        // ── Yordamchi ──────────────────────────────────────────────────

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

    // ── MQTT Payload modellari ─────────────────────────────────────────

    internal sealed class DeviceConnectedPayload
    {
        public string SessionToken { get; set; } = string.Empty;
    }

    internal sealed class TelemetryPayload
    {
        public string SessionToken { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
    }

    internal sealed class SessionCompletedPayload
    {
        public string SessionToken { get; set; } = string.Empty;
        public decimal FinalQuantity { get; set; }
        public string? EndReason { get; set; }
    }
}
