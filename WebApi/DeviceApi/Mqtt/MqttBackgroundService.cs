using System.Text;
using System.Text.Json;
using DeviceApi.Clients;
using DeviceApi.Mqtt.Payloads;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;

namespace DeviceApi.Mqtt
{
    public class MqttBackgroundService : BackgroundService
    {
        private readonly IUserApiClient _userApiClient;
        private readonly MqttOptions _options;
        private readonly ILogger<MqttBackgroundService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        public MqttBackgroundService(
            IUserApiClient userApiClient,
            IOptions<MqttOptions> options,
            ILogger<MqttBackgroundService> logger)
        {
            _userApiClient = userApiClient;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new MqttFactory();
            using var mqttClient = factory.CreateMqttClient();

            mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!mqttClient.IsConnected)
                    {
                        var optionsBuilder = new MqttClientOptionsBuilder()
                            .WithTcpServer(_options.BrokerHost, _options.BrokerPort)
                            .WithClientId(_options.ClientId);

                        if (!string.IsNullOrEmpty(_options.Username))
                            optionsBuilder = optionsBuilder.WithCredentials(_options.Username, _options.Password);

                        await mqttClient.ConnectAsync(optionsBuilder.Build(), stoppingToken);

                        _logger.LogInformation("MQTT brokerga ulandi: {Host}:{Port}", _options.BrokerHost, _options.BrokerPort);

                        await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic("devices/+/connect").Build(), stoppingToken);
                        await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic("devices/+/progress").Build(), stoppingToken);
                        await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic("devices/+/finish").Build(), stoppingToken);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MQTT ulanish xatosi. 10 soniyadan keyin qayta uriniladi.");
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }

            if (mqttClient.IsConnected)
                await mqttClient.DisconnectAsync();
        }

        private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            var topic = args.ApplicationMessage.Topic;
            var payloadBytes = args.ApplicationMessage.PayloadSegment;
            var payload = Encoding.UTF8.GetString(payloadBytes);

            // Topic format: devices/{serialNumber}/{action}
            var parts = topic.Split('/');
            if (parts.Length != 3 || parts[0] != "devices")
                return;

            var serialNumber = parts[1];
            var action = parts[2];

            try
            {
                switch (action)
                {
                    case "connect":
                    {
                        var data = JsonSerializer.Deserialize<DeviceConnectPayload>(payload, JsonOptions);
                        if (data is not null)
                            await _userApiClient.DeviceConnectAsync(serialNumber, data.SessionToken, data.ProductType);
                        break;
                    }
                    case "progress":
                    {
                        var data = JsonSerializer.Deserialize<DeviceProgressPayload>(payload, JsonOptions);
                        if (data is not null)
                            await _userApiClient.DeviceProgressAsync(serialNumber, data.SessionToken, data.Quantity, data.TotalQuantity);
                        break;
                    }
                    case "finish":
                    {
                        var data = JsonSerializer.Deserialize<DeviceFinishPayload>(payload, JsonOptions);
                        if (data is not null)
                            await _userApiClient.DeviceFinishAsync(serialNumber, data.SessionToken, data.FinalQuantity);
                        break;
                    }
                    default:
                        _logger.LogWarning("Noma'lum MQTT action: {Action}, Serial: {Serial}", action, serialNumber);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MQTT xabarini qayta ishlashda xato. Topic: {Topic}", topic);
            }
        }
    }
}
