using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using CommonConfiguration.Messaging;
using SessionApi.Services;
using Domain.Interfaces;
using Domain.Messaging;
using Domain.Messaging.Events;
using Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;

namespace SessionApi.Mqtt
{
    /// <summary>
    /// MQTT bilan bevosita bog'lanish — qurilmalardan event/telemetry/heartbeat qabul qiladi,
    /// qurilmalarga buyruq yuboradi. Qurilma eventlarini RabbitMQ ga uzatadi.
    ///
    /// <para><b>Wire format (har ikki yo'nalish ham):</b></para>
    /// <code>
    /// {
    ///   "envelope": { "id": &lt;long&gt;, "payload": { /* topic-specific */ } },
    ///   "hmac": "&lt;base64 HMAC-SHA256&gt;"
    /// }
    /// </code>
    ///
    /// <para><b>Topiclar:</b></para>
    /// <list type="bullet">
    /// <item><c>device/{serial}/connect</c>     — payload: <c>{ user_id, session_token }</c></item>
    /// <item><c>device/{serial}/event</c>       — payload: <c>{ type, session_token, process_id?, final_quantity? }</c></item>
    /// <item><c>device/{serial}/telemetry</c>   — payload: <c>{ session_token, process_id, sequence, quantity }</c></item>
    /// <item><c>device/{serial}/response</c>    — payload: <c>{ process_id, command, status }</c> (diagnostika)</item>
    /// <item><c>device/{serial}/heartbeat</c>   — payload: <c>{}</c></item>
    /// <item><c>device/{serial}/payment_qr</c>  — payload: <c>{ session_token, amount, payme_token, client_ref? }</c></item>
    /// <item><c>device/{serial}/status</c>      — diagnostika, envelope talab qilmaydi (plain JSON)</item>
    /// <item><c>server/{serial}/command</c>     — server → qurilma buyruqlari</item>
    /// <item><c>server/{serial}/connect_ack</c> — connect natijasi (id request id'ni echo qiladi)</item>
    /// <item><c>server/{serial}/payment_result</c> — to'lov natijasi</item>
    /// </list>
    ///
    /// <para><b>Xavfsizlik qatlamlari</b>: TLS (transport) + HMAC-SHA256 (per-message auth/integrity)
    /// + monotonic id (replay protection inbound). HMAC kalit qurilmaning <c>SecretKey</c> dan
    /// <c>SHA-256("BOT-ENERGY-MQTT-HMAC:" + key)</c> orqali derive qilinadi.</para>
    /// </summary>
    public sealed class MqttBridge : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly RabbitMqPublisher _rabbitPublisher;
        private readonly MqttOptions _options;
        private readonly ILogger<MqttBridge> _logger;
        private IMqttClient? _mqttClient;

        private static readonly JsonSerializerOptions JsonOpts = MqttMessageEnvelope.JsonOpts;

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
                            "MQTT brokerga ulandi: {Host}:{Port} (TLS={Tls})",
                            _options.BrokerHost, _options.BrokerPort, _options.UseTls);

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
                    tls.WithClientCertificates(new[] { clientCert });
            });
        }

        // ── Qurilmaga buyruq yuborish (unsolicited — server'ning o'z counterini ishlatadi) ──

        public Task PublishStartCommandAsync(string serialNumber, long processId, long productId, decimal amount)
            => PublishUnsolicitedAsync(serialNumber, "command", new
            {
                type = "start",
                process_id = processId,
                product_id = productId,
                amount
            });

        public Task PublishPauseCommandAsync(string serialNumber, long processId)
            => PublishUnsolicitedAsync(serialNumber, "command", new { type = "pause", process_id = processId });

        public Task PublishResumeCommandAsync(string serialNumber, long processId)
            => PublishUnsolicitedAsync(serialNumber, "command", new { type = "resume", process_id = processId });

        public Task PublishStopCommandAsync(string serialNumber, long processId)
            => PublishUnsolicitedAsync(serialNumber, "command", new { type = "stop", process_id = processId });

        public Task PublishPaymentResultAsync(
            string serialNumber, long transactionId, string status,
            decimal amount, decimal? newBalance, string? message, string? clientRef)
            => PublishUnsolicitedAsync(serialNumber, "payment_result", new
            {
                transaction_id = transactionId,
                status,
                amount,
                new_balance = newBalance,
                message,
                client_ref = clientRef
            });

        /// <summary>
        /// Connect oqimi javobi — request id <paramref name="requestId"/> echo qilinadi (correlation).
        /// </summary>
        public Task PublishConnectAckAsync(string serialNumber, long requestId, MqttResultEnvelope<ConnectAckData> result)
        {
            _logger.LogInformation(
                "[CONNECT-ACK] serial={Serial} id={Id} ok={Ok} code={Code} sessionId={SessionId}",
                serialNumber, requestId, result.Ok, result.Code, result.Data?.SessionId);

            return PublishResponseAsync(serialNumber, "connect_ack", requestId, result);
        }

        // ── MQTT xabarlarni qabul qilish → envelope unwrap → handler ──────

        private async Task OnMessageAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            var topic = args.ApplicationMessage.Topic;
            var rawPayload = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);

            _logger.LogInformation("[MQTT-IN] Topic={Topic} payloadLen={Len}", topic, rawPayload.Length);

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
                // Diagnostika topic'i — envelope/HMAC talab qilinmaydi.
                if (action == "status")
                {
                    HandleDeviceStatus(serialNumber, rawPayload);
                    return;
                }

                using var scope = _scopeFactory.CreateScope();
                var deviceRepo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
                var idStore = scope.ServiceProvider.GetRequiredService<IMqttMessageIdStore>();

                var device = await deviceRepo.GetBySerialNumberAsync(serialNumber);
                if (device is null)
                {
                    _logger.LogWarning(
                        "[MQTT-IN] Qurilma topilmadi serial={Serial} topic={Topic}", serialNumber, topic);
                    return; // ack qaytarmaymiz — kim so'rayotganini autentifikatsiya qilolmaymiz
                }

                if (!MqttMessageEnvelope.TryUnwrap(
                        rawPayload, device.SecretKey,
                        out var messageId, out var payloadJson, out var unwrapError))
                {
                    _logger.LogWarning(
                        "[MQTT-IN] Envelope rad etildi reason={Reason} serial={Serial} topic={Topic}",
                        unwrapError, serialNumber, topic);
                    // Auth o'tmadi — ack qaytarmaymiz (kim so'rayotganini bilolmaymiz)
                    return;
                }

                if (!await idStore.TryAcceptInboundIdAsync(serialNumber, messageId))
                {
                    _logger.LogWarning(
                        "[MQTT-IN] Replay rad etildi id={Id} (avvalgisi >= bu qiymat) serial={Serial}",
                        messageId, serialNumber);
                    return;
                }

                await deviceRepo.TouchLastSeenAsync(serialNumber);

                _logger.LogInformation(
                    "[MQTT-IN] Envelope OK id={Id} action={Action} serial={Serial} payloadLen={Len}",
                    messageId, action, serialNumber, payloadJson.Length);

                switch (action)
                {
                    case "connect":
                        await HandleConnectAsync(serialNumber, messageId, payloadJson);
                        break;

                    case "event":
                        HandleDeviceEvent(serialNumber, payloadJson);
                        break;

                    case "telemetry":
                        HandleTelemetry(serialNumber, payloadJson);
                        break;

                    case "response":
                        HandleDeviceResponse(serialNumber, payloadJson);
                        break;

                    case "heartbeat":
                        HandleHeartbeat(serialNumber);
                        break;

                    case "payment_qr":
                        HandlePaymentQr(serialNumber, payloadJson);
                        break;

                    default:
                        _logger.LogDebug("Noma'lum MQTT action: {Topic}", topic);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MQTT xabar qayta ishlashda xato. Topic: {Topic}", topic);
            }
        }

        private async Task HandleConnectAsync(string serialNumber, long requestId, string payloadJson)
        {
            _logger.LogInformation(
                "[CONNECT] device/{Serial}/connect id={Id} payload: {Payload}",
                serialNumber, requestId, payloadJson);

            DeviceConnectPayload? data;
            try
            {
                data = JsonSerializer.Deserialize<DeviceConnectPayload>(payloadJson, JsonOpts);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex,
                    "[CONNECT] Payload JSON parse xatoligi serial={Serial}", serialNumber);
                await PublishConnectAckAsync(serialNumber, requestId, MqttResultEnvelope.Fail<ConnectAckData>(
                    ConnectResultCodes.InvalidPayload, "Payload JSON formati buzuq."));
                return;
            }

            if (data is null || data.UserId is null or 0 || string.IsNullOrEmpty(data.SessionToken))
            {
                _logger.LogWarning(
                    "[CONNECT] Payload yaroqsiz — userId={UserId} tokenEmpty={Empty} serial={Serial}",
                    data?.UserId, string.IsNullOrEmpty(data?.SessionToken), serialNumber);
                await PublishConnectAckAsync(serialNumber, requestId, MqttResultEnvelope.Fail<ConnectAckData>(
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

            await PublishConnectAckAsync(serialNumber, requestId, envelope);
        }

        private void HandleDeviceEvent(string serialNumber, string payloadJson)
        {
            var data = JsonSerializer.Deserialize<DeviceEventPayload>(payloadJson, JsonOpts);
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

        private void HandleTelemetry(string serialNumber, string payloadJson)
        {
            var data = JsonSerializer.Deserialize<TelemetryPayload>(payloadJson, JsonOpts);
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

        private void HandleDeviceResponse(string serialNumber, string payloadJson)
        {
            // Response qurilmaning command-ga ack-i — hozircha faqat log.
            // Kelajakda command tracking jadvali kerak bo'lganda bu yerda yangilanadi.
            var data = JsonSerializer.Deserialize<DeviceResponsePayload>(payloadJson, JsonOpts);
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

        private void HandlePaymentQr(string serialNumber, string payloadJson)
        {
            var data = JsonSerializer.Deserialize<PaymentQrPayload>(payloadJson, JsonOpts);
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
                    "Yaroqsiz payment_qr payload — Serial: {Serial}, Amount={Amount}",
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

        // ── Outbound: envelope+HMAC bilan o'rab MQTT'ga yuborish ──────────

        private async Task PublishUnsolicitedAsync<T>(string serialNumber, string action, T payload)
        {
            using var scope = _scopeFactory.CreateScope();
            var idStore = scope.ServiceProvider.GetRequiredService<IMqttMessageIdStore>();
            var id = await idStore.NextOutboundIdAsync(serialNumber);
            await PublishToDeviceAsync(serialNumber, action, id, payload);
        }

        private Task PublishResponseAsync<T>(string serialNumber, string action, long correlatedId, T payload)
            => PublishToDeviceAsync(serialNumber, action, correlatedId, payload);

        private async Task PublishToDeviceAsync<T>(string serialNumber, string action, long id, T payload)
        {
            if (_mqttClient is null || !_mqttClient.IsConnected)
            {
                _logger.LogWarning(
                    "MQTT ulanmagan. Yuborilmadi: {Serial}/{Action} id={Id}", serialNumber, action, id);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var deviceRepo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
            var device = await deviceRepo.GetBySerialNumberAsync(serialNumber);
            if (device is null)
            {
                _logger.LogWarning(
                    "MQTT publish rad etildi — qurilma topilmadi serial={Serial}", serialNumber);
                return;
            }

            var envelopeJson = MqttMessageEnvelope.Wrap(id, payload, device.SecretKey);

            var topic = $"server/{serialNumber}/{action}";
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(envelopeJson)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag(false)
                .Build();

            await _mqttClient.PublishAsync(message);
            _logger.LogDebug("[MQTT-OUT] {Topic} id={Id} ({Len} bytes)", topic, id, envelopeJson.Length);
        }
    }

    // ── MQTT Payload modellari (envelope.payload mazmuni) ──────────────────

    /// <summary>device/{serial}/connect payload</summary>
    internal sealed class DeviceConnectPayload
    {
        public long? UserId { get; set; }
        public string? SessionToken { get; set; }
    }

    /// <summary>device/{serial}/event payload (jarayon tugashi)</summary>
    internal sealed class DeviceEventPayload
    {
        public string? Type { get; set; }
        public string? SessionToken { get; set; }
        public long? ProcessId { get; set; }
        public decimal? FinalQuantity { get; set; }
    }

    /// <summary>device/{serial}/telemetry payload</summary>
    internal sealed class TelemetryPayload
    {
        public string SessionToken { get; set; } = string.Empty;
        public long ProcessId { get; set; }
        public long Sequence { get; set; }
        public decimal Quantity { get; set; }
    }

    /// <summary>device/{serial}/response payload (command ack)</summary>
    internal sealed class DeviceResponsePayload
    {
        public long ProcessId { get; set; }
        public string? Command { get; set; }
        public string? Status { get; set; }
    }

    /// <summary>device/{serial}/payment_qr payload</summary>
    internal sealed class PaymentQrPayload
    {
        public string SessionToken { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string PaymeToken { get; set; } = string.Empty;
        public string? ClientRef { get; set; }
    }

    /// <summary>
    /// server/{serial}/connect_ack envelope ichidagi data. Success holatda <c>SessionId</c>
    /// to'ldiriladi, Fail holatda envelope.Data null bo'ladi.
    /// </summary>
    public sealed record ConnectAckData(long SessionId);
}
