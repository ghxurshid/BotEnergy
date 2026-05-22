using System.Text;
using Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet.Client;
using MQTTnet.Protocol;
using SessionApi.Mqtt.Abstractions;
using SessionApi.Mqtt.Pipeline;
using SessionApi.Mqtt.Topics;

namespace SessionApi.Mqtt.Transport
{
    /// <summary>
    /// MQTT broker bilan connection lifecycle'ni yuritadi va inbound xabarlarni pipeline'ga uzatadi.
    /// Eski <c>MqttBridge</c> ning yagona vazifasini pipeline + handler arxitekturasiga bo'lib oladi.
    /// </summary>
    public sealed class MqttHost : BackgroundService
    {
        private readonly MqttConnection _connection;
        private readonly MqttPipeline _pipeline;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MqttHost> _logger;

        public MqttHost(
            MqttConnection connection,
            MqttPipeline pipeline,
            IServiceScopeFactory scopeFactory,
            ILogger<MqttHost> logger)
        {
            _connection = connection;
            _pipeline = pipeline;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _connection.MessageReceived = OnMessageAsync;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!_connection.IsConnected)
                    {
                        await _connection.ConnectAsync(stoppingToken);
                        await SubscribeAllAsync(stoppingToken);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MQTT ulanish xatosi. 5s dan keyin qayta uriniladi.");
                    try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
                    catch (OperationCanceledException) { break; }
                }
            }

            await _connection.DisposeAsync();
        }

        private async Task SubscribeAllAsync(CancellationToken ct)
        {
            await _connection.SubscribeAsync(MqttTopics.DeviceRequestSub, MqttQualityOfServiceLevel.AtLeastOnce, ct);
            await _connection.SubscribeAsync(MqttTopics.DeviceResponseSub, MqttQualityOfServiceLevel.AtLeastOnce, ct);
            await _connection.SubscribeAsync(MqttTopics.DeviceEventSub, MqttQualityOfServiceLevel.AtLeastOnce, ct);
            await _connection.SubscribeAsync(MqttTopics.DeviceTelemetrySub, MqttQualityOfServiceLevel.AtMostOnce, ct);
            await _connection.SubscribeAsync(MqttTopics.DeviceStateSub, MqttQualityOfServiceLevel.AtLeastOnce, ct);
        }

        private async Task OnMessageAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            var topic = args.ApplicationMessage.Topic;
            var rawJson = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);

            var parsed = MqttTopics.Parse(topic);
            if (parsed is null)
            {
                _logger.LogWarning("[MQTT-IN] Topic noto'g'ri formatda: {Topic}", topic);
                return;
            }

            using var scope = _scopeFactory.CreateScope();

            var context = new MqttContext
            {
                Topic = topic,
                SerialNumber = parsed.SerialNumber,
                TopicKind = parsed.Kind,
                RawJson = rawJson,
                Services = scope.ServiceProvider,
                CancellationToken = CancellationToken.None
            };

            try
            {
                await _pipeline.RunAsync(context);

                if (context.Device is not null)
                {
                    var deviceRepo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
                    await deviceRepo.TouchLastSeenAsync(parsed.SerialNumber);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[MQTT-IN] Pipeline ishlatishda kutilmagan xato topic={Topic}",
                    topic);
            }
        }
    }
}
