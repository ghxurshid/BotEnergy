using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace SessionApi.Mqtt.Transport
{
    /// <summary>
    /// MQTTnet client wrapper. Connection lifecycle, TLS, subscribe va publish'ni inkapsulyatsiya qiladi.
    /// <see cref="MqttHost"/> reconnect tsiklini boshqaradi, <see cref="MqttPublisher"/> publish chaqiradi.
    /// </summary>
    public sealed class MqttConnection : IAsyncDisposable
    {
        private readonly IMqttClient _client;
        private readonly MqttOptions _options;
        private readonly ILogger<MqttConnection> _logger;

        public MqttConnection(IOptions<MqttOptions> options, ILogger<MqttConnection> logger)
        {
            _options = options.Value;
            _logger = logger;
            _client = new MqttFactory().CreateMqttClient();
            _client.ApplicationMessageReceivedAsync += async args =>
            {
                if (MessageReceived is not null)
                    await MessageReceived(args);
            };
        }

        /// <summary>
        /// Broker'dan inbound xabar kelganda raised bo'ladi. <see cref="MqttHost"/> shu yerga subscribe qiladi.
        /// </summary>
        public Func<MqttApplicationMessageReceivedEventArgs, Task>? MessageReceived { get; set; }

        public bool IsConnected => _client.IsConnected;

        public async Task ConnectAsync(CancellationToken ct)
        {
            var opts = new MqttClientOptionsBuilder()
                .WithTcpServer(_options.BrokerHost, _options.BrokerPort)
                .WithClientId(_options.ClientId)
                .WithCleanSession(false);

            if (!string.IsNullOrEmpty(_options.Username))
                opts = opts.WithCredentials(_options.Username, _options.Password);

            if (_options.UseTls)
                opts = ApplyTls(opts);

            await _client.ConnectAsync(opts.Build(), ct);

            _logger.LogInformation(
                "MQTT brokerga ulandi: {Host}:{Port} (TLS={Tls})",
                _options.BrokerHost, _options.BrokerPort, _options.UseTls);
        }

        public async Task SubscribeAsync(string topic, MqttQualityOfServiceLevel qos, CancellationToken ct)
        {
            await _client.SubscribeAsync(
                new MqttTopicFilterBuilder().WithTopic(topic).WithQualityOfServiceLevel(qos).Build(),
                ct);
            _logger.LogDebug("MQTT subscribe: {Topic} qos={Qos}", topic, qos);
        }

        public async Task PublishAsync(string topic, string payload, MqttQualityOfServiceLevel qos, CancellationToken ct)
        {
            if (!_client.IsConnected)
            {
                _logger.LogWarning("MQTT ulanmagan — publish rad etildi topic={Topic}", topic);
                return;
            }

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(qos)
                .WithRetainFlag(false)
                .Build();

            await _client.PublishAsync(message, ct);
            _logger.LogDebug("[MQTT-OUT] {Topic} ({Len} bytes)", topic, payload.Length);
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

        public async ValueTask DisposeAsync()
        {
            if (_client.IsConnected)
            {
                try { await _client.DisconnectAsync(); }
                catch (Exception ex) { _logger.LogWarning(ex, "MQTT disconnect xatosi"); }
            }
            _client.Dispose();
        }
    }
}
