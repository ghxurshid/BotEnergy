using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using CommonConfiguration.Messaging;
using DeviceApi.Services;
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
    ///                    server/{serial}/connect_ack — sessiyaga ulanish urinishi natijasi
    ///                    server/{serial}/payment_result
    ///  Device → Server:  device/{serial}/connect    — sessiyaga ulanish so'rovi ({ userId, sessionToken })
    ///                    device/{serial}/event       — stopped / error / out_of_resource / completed (jarayon yakuni)
    ///                    device/{serial}/telemetry  — har 5s
    ///                    device/{serial}/response   — buyruqlarga ack
    ///                    device/{serial}/heartbeat  — har 30s, LastSeenAt yangilanishi uchun
    ///                    device/{serial}/status     — diagnostika (auth/encryption talab qilinmaydi)
    ///                    device/{serial}/payment_qr — QR to'lov so'rovi
    ///
    /// **Xavfsizlik qatlamlari (sozlamaga ko'ra):**
    ///  - TLS (UseTls=true) — MQTT broker bilan shifrlangan TCP
    ///  - Mutual TLS (ClientCertificatePath bo'lsa) — qurilma sertifikati bilan
    ///  - Application-level (EnableEncryption=true): AES-256-GCM + HMAC-SHA256 + timestamp
    ///    Kalitlar Device.SecretKey dan derive qilinadi (har qurilma uchun unikal).
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

                        if (_options.UseTls)
                            opts = ApplyTls(opts);

                        await _mqttClient.ConnectAsync(opts.Build(), stoppingToken);

                        _logger.LogInformation(
                            "MQTT brokerga ulandi: {Host}:{Port} (TLS={Tls}, Encryption={Enc})",
                            _options.BrokerHost, _options.BrokerPort, _options.UseTls, _options.EnableEncryption);

                        await _mqttClient.SubscribeAsync(
                            new MqttTopicFilterBuilder().WithTopic("device/+/connect").WithAtLeastOnceQoS().Build(), stoppingToken);
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
                        await _mqttClient.SubscribeAsync(
                            new MqttTopicFilterBuilder().WithTopic("device/+/payment_qr").WithAtLeastOnceQoS().Build(), stoppingToken);
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

        private MqttClientOptionsBuilder ApplyTls(MqttClientOptionsBuilder builder)
        {
            X509Certificate2? clientCert = null;
            if (!string.IsNullOrEmpty(_options.ClientCertificatePath))
            {
                clientCert = new X509Certificate2(
                    _options.ClientCertificatePath,
                    _options.ClientCertificatePassword);
            }

            return builder.WithTlsOptions(tls =>
            {
                tls.UseTls(true);
                tls.WithAllowUntrustedCertificates(_options.AllowUntrustedCertificates);
                tls.WithIgnoreCertificateChainErrors(_options.AllowUntrustedCertificates);
                tls.WithIgnoreCertificateRevocationErrors(_options.AllowUntrustedCertificates);
                tls.WithSslProtocols(SslProtocols.Tls12);

                if (clientCert is not null)
                {
                    tls.WithClientCertificates(new[] { clientCert });
                }
            });
        }

        // ── Qurilmaga buyruq yuborish (RabbitMQ dan keladi) ───────────

        public Task PublishStartCommandAsync(string serialNumber, long processId, long productId, decimal amount)
            => PublishToDeviceAsync(serialNumber, "command", new
            {
                type = "start",
                process_id = processId,
                product_id = productId,
                amount
            });

        public Task PublishPauseCommandAsync(string serialNumber, long processId)
            => PublishToDeviceAsync(serialNumber, "command", new
            {
                type = "pause",
                process_id = processId
            });

        public Task PublishResumeCommandAsync(string serialNumber, long processId)
            => PublishToDeviceAsync(serialNumber, "command", new
            {
                type = "resume",
                process_id = processId
            });

        public Task PublishStopCommandAsync(string serialNumber, long processId)
            => PublishToDeviceAsync(serialNumber, "command", new
            {
                type = "stop",
                process_id = processId
            });

        /// <summary>
        /// Sessiya ulanish urinishi natijasini qurilmaga qaytaradi (server/{serial}/connect_ack)
        /// standart <see cref="MqttResultEnvelope{T}"/> shaklida. Qurilma displey
        /// <c>code</c> bo'yicha tegishli ekranni ko'rsatadi, <c>message</c> debug uchun.
        /// </summary>
        public Task PublishConnectAckAsync(string serialNumber, MqttResultEnvelope<ConnectAckData> envelope)
        {
            _logger.LogInformation(
                "[CONNECT-ACK] serial={Serial} ok={Ok} code={Code} sessionId={SessionId}",
                serialNumber, envelope.Ok, envelope.Code, envelope.Data?.SessionId);

            return PublishToDeviceAsync(serialNumber, "connect_ack", envelope);
        }

        /// <summary>
        /// To'lov natijasini qurilma displeyiga yuboradi (server/{serial}/payment_result).
        /// </summary>
        public Task PublishPaymentResultAsync(
            string serialNumber,
            long transactionId,
            string status,
            decimal amount,
            decimal? newBalance,
            string? message,
            string? clientRef)
            => PublishToDeviceAsync(serialNumber, "payment_result", new
            {
                transaction_id = transactionId,
                status,
                amount,
                new_balance = newBalance,
                message,
                client_ref = clientRef
            });

        // ── MQTT xabarlarni qabul qilish → RabbitMQ ga uzatish ───────

        private async Task OnMessageAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            var topic = args.ApplicationMessage.Topic;
            var rawPayload = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);

            _logger.LogInformation(
                "[MQTT-IN] Topic={Topic} payloadLen={Len} encryption={Enc}",
                topic, rawPayload.Length, _options.EnableEncryption);

            var parts = topic.Split('/');
            if (parts.Length < 3 || parts[0] != "device")
            {
                _logger.LogWarning(
                    "[MQTT-IN] Topic prefiksi noto'g'ri — kutilgan: 'device/{{serial}}/{{action}}'. Olingan: {Topic}",
                    topic);
                return;
            }

            var serialNumber = parts[1];
            var action = parts[2];

            try
            {
                string payload;

                if (action == "status")
                {
                    // Diagnostika — auth/encryption talab qilmaydi
                    payload = rawPayload;
                }
                else
                {
                    using var scope = _scopeFactory.CreateScope();
                    var deviceRepo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();

                    if (_options.EnableEncryption)
                    {
                        // Encrypted flow: serial → SecretKey → envelope unwrap
                        var device = await deviceRepo.GetBySerialNumberAsync(serialNumber);
                        if (device is null)
                        {
                            _logger.LogWarning(
                                "[MQTT-IN] Qurilma topilmadi serial={Serial} topic={Topic}",
                                serialNumber, topic);

                            // connect topic'iga noma'lum serial kelsa, ack qaytarish foydasiz —
                            // qurilma ro'yxatga olinmagan, SecretKey ham bilmaymiz.
                            return;
                        }

                        if (!SecureMqttEnvelope.TryUnwrap(
                                rawPayload,
                                device.SecretKey,
                                _options.MaxClockSkewSeconds,
                                out payload,
                                out var error))
                        {
                            _logger.LogWarning(
                                "[MQTT-IN] Envelope rad etildi reason={Reason} serial={Serial} topic={Topic}",
                                error, serialNumber, topic);

                            if (action == "connect")
                                await PublishConnectAckAsync(serialNumber, MqttResultEnvelope.Fail<ConnectAckData>(
                                    ConnectResultCodes.InvalidPayload, "Envelope shifrlanmagan yoki yaroqsiz."));
                            return;
                        }

                        // Envelope HMAC+GCM tag muvaffaqiyatli — identity tasdiqlandi.
                        await deviceRepo.TouchLastSeenAsync(serialNumber);
                        _logger.LogDebug("[MQTT-IN] Envelope unwrap OK serial={Serial}", serialNumber);
                    }
                    else
                    {
                        // Legacy flow: payload ichida device_token bo'lishi shart
                        var basePayload = JsonSerializer.Deserialize<DeviceAuthPayload>(rawPayload, JsonOpts);
                        if (string.IsNullOrEmpty(basePayload?.DeviceToken))
                        {
                            _logger.LogWarning(
                                "[MQTT-IN] device_token yo'q topic={Topic}", topic);

                            if (action == "connect")
                                await PublishConnectAckAsync(serialNumber, MqttResultEnvelope.Fail<ConnectAckData>(
                                    ConnectResultCodes.InvalidPayload, "device_token field yo'q."));
                            return;
                        }

                        var isValid = await deviceRepo.ValidateDeviceAsync(serialNumber, basePayload.DeviceToken);
                        if (!isValid)
                        {
                            _logger.LogWarning(
                                "[MQTT-IN] device_token yaroqsiz serial={Serial} topic={Topic}",
                                serialNumber, topic);

                            if (action == "connect")
                                await PublishConnectAckAsync(serialNumber, MqttResultEnvelope.Fail<ConnectAckData>(
                                    ConnectResultCodes.InvalidPayload, "device_token noto'g'ri."));
                            return;
                        }

                        await deviceRepo.TouchLastSeenAsync(serialNumber);
                        payload = rawPayload;
                        _logger.LogDebug("[MQTT-IN] Legacy auth OK serial={Serial}", serialNumber);
                    }
                }

                switch (action)
                {
                    case "connect":
                        await HandleConnectAsync(serialNumber, payload);
                        break;

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

                    case "payment_qr":
                        HandlePaymentQr(serialNumber, payload);
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

        private async Task HandleConnectAsync(string serialNumber, string payload)
        {
            _logger.LogInformation(
                "[CONNECT] device/{Serial}/connect payload: {Payload}", serialNumber, payload);

            DeviceConnectPayload? data;
            try
            {
                data = JsonSerializer.Deserialize<DeviceConnectPayload>(payload, JsonOpts);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex,
                    "[CONNECT] Payload JSON parse xatoligi serial={Serial}", serialNumber);
                await PublishConnectAckAsync(serialNumber, MqttResultEnvelope.Fail<ConnectAckData>(
                    ConnectResultCodes.InvalidPayload, "Payload JSON formati buzuq."));
                return;
            }

            if (data is null || data.UserId is null or 0 || string.IsNullOrEmpty(data.SessionToken))
            {
                _logger.LogWarning(
                    "[CONNECT] Payload yaroqsiz — userId={UserId} tokenEmpty={Empty} serial={Serial}",
                    data?.UserId, string.IsNullOrEmpty(data?.SessionToken), serialNumber);
                await PublishConnectAckAsync(serialNumber, MqttResultEnvelope.Fail<ConnectAckData>(
                    ConnectResultCodes.InvalidPayload,
                    "user_id (>0) va session_token (bo'sh emas) majburiy. Snake_case formatda yuboring."));
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var sessionService = scope.ServiceProvider.GetRequiredService<IDeviceSessionService>();

            var result = await sessionService.TryConnectAsync(
                serialNumber, data.UserId.Value, data.SessionToken, CancellationToken.None);

            var envelope = result.Success
                ? MqttResultEnvelope.Success(result.Code, result.Message, new ConnectAckData(result.SessionId!.Value))
                : MqttResultEnvelope.Fail<ConnectAckData>(result.Code, result.Message);

            await PublishConnectAckAsync(serialNumber, envelope);
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

        private void HandlePaymentQr(string serialNumber, string payload)
        {
            var data = JsonSerializer.Deserialize<PaymentQrPayload>(payload, JsonOpts);
            if (data is null)
            {
                _logger.LogWarning("Bo'sh payment_qr payload — Serial: {Serial}", serialNumber);
                return;
            }

            if (string.IsNullOrEmpty(data.SessionToken) ||
                string.IsNullOrEmpty(data.PaymeToken) ||
                data.Amount <= 0)
            {
                _logger.LogWarning(
                    "Yaroqsiz payment_qr payload — Serial: {Serial}, SessionToken bo'shmi/PaymeToken bo'shmi/Amount {Amount}",
                    serialNumber, data.Amount);
                return;
            }

            _rabbitPublisher.Publish(QueueNames.PaymentEventQueue, new DevicePaymentRequest
            {
                SerialNumber = serialNumber,
                SessionToken = data.SessionToken,
                Amount = data.Amount,
                PaymeToken = data.PaymeToken,
                ClientRef = data.ClientRef
            });
        }

        // ── Yordamchi ─────────────────────────────────────────────────

        private async Task PublishToDeviceAsync<T>(string serialNumber, string action, T payload)
        {
            if (_mqttClient is null || !_mqttClient.IsConnected)
            {
                _logger.LogWarning("MQTT ulanmagan. Buyruq yuborilmadi: {Serial}/{Action}", serialNumber, action);
                return;
            }

            var topic = $"server/{serialNumber}/{action}";
            var json = JsonSerializer.Serialize(payload, JsonOpts);

            string finalPayload;
            if (_options.EnableEncryption)
            {
                using var scope = _scopeFactory.CreateScope();
                var deviceRepo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
                var device = await deviceRepo.GetBySerialNumberAsync(serialNumber);
                if (device is null)
                {
                    _logger.LogWarning(
                        "MQTT publish rad etildi — qurilma topilmadi: {Serial}", serialNumber);
                    return;
                }

                finalPayload = SecureMqttEnvelope.Wrap(json, device.SecretKey);
            }
            else
            {
                finalPayload = json;
            }

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(finalPayload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag(false)
                .Build();

            await _mqttClient.PublishAsync(message);
            _logger.LogDebug("MQTT publish: {Topic} ({Len} bytes)", topic, finalPayload.Length);
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

    /// <summary>
    /// device/{serial}/connect topic'idan keladi. Qurilma reader QR'dan o'qigan
    /// user_id va session_token'ni jamlaydi.
    /// </summary>
    internal sealed class DeviceConnectPayload : DeviceAuthPayload
    {
        public long? UserId { get; set; }
        public string? SessionToken { get; set; }
    }

    /// <summary>
    /// server/{serial}/connect_ack envelope ichidagi data. Success holatda <c>SessionId</c>
    /// to'ldiriladi, Fail holatda envelope.Data null bo'ladi.
    /// </summary>
    public sealed record ConnectAckData(long SessionId);

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

    internal sealed class PaymentQrPayload : DeviceAuthPayload
    {
        public string SessionToken { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string PaymeToken { get; set; } = string.Empty;
        public string? ClientRef { get; set; }
    }
}
