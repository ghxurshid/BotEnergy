using System.Diagnostics;
using System.Text;
using CommonConfiguration.Observability;
using Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet.Client;
using MQTTnet.Protocol;
using SessionApi.Mqtt.Abstractions;
using SessionApi.Mqtt.Pipeline;
using SessionApi.Mqtt.Topics;

namespace SessionApi.Mqtt.Transport
{
    /// <summary>
    /// MQTT broker bilan connection lifecycle'ni yuritadi va inbound xabarlarni pipeline'ga uzatadi.
    /// Kiruvchi xabarlar pipeline + handler arxitekturasi orqali qayta ishlanadi.
    /// </summary>
    public sealed class MqttHost : BackgroundService
    {
        private readonly MqttConnection _connection;
        private readonly MqttPipeline _pipeline;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly MqttOptions _options;
        private readonly ILogger<MqttHost> _logger;

        public MqttHost(
            MqttConnection connection,
            MqttPipeline pipeline,
            IServiceScopeFactory scopeFactory,
            IOptions<MqttOptions> options,
            ILogger<MqttHost> logger)
        {
            _connection = connection;
            _pipeline = pipeline;
            _scopeFactory = scopeFactory;
            _options = options.Value;
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
            // Shared subscription (Mqtt:SharedSubscriptionGroup) yoqilgan bo'lsa har bir xabarni
            // guruhdagi FAQAT BITTA instansiya oladi — SessionApi'ni horizontal masshtablash
            // shunga tayanadi. Guruh bo'sh bo'lsa (dev, Mosquitto) oddiy obuna ishlatiladi.
            await _connection.SubscribeAsync(
                _options.SubscriptionTopic(MqttTopics.DeviceRequestSub), MqttQualityOfServiceLevel.AtLeastOnce, ct);
            await _connection.SubscribeAsync(
                _options.SubscriptionTopic(MqttTopics.DeviceResponseSub), MqttQualityOfServiceLevel.AtLeastOnce, ct);
            await _connection.SubscribeAsync(
                _options.SubscriptionTopic(MqttTopics.DeviceEventSub), MqttQualityOfServiceLevel.AtLeastOnce, ct);
            await _connection.SubscribeAsync(
                _options.SubscriptionTopic(MqttTopics.DeviceTelemetrySub), MqttQualityOfServiceLevel.AtMostOnce, ct);

            // state — retained snapshot. Har bir instansiya joriy holatni bilishi kerak,
            // shuning uchun bu obuna HECH QACHON shared qilinmaydi.
            await _connection.SubscribeAsync(
                MqttTopics.DeviceStateSub, MqttQualityOfServiceLevel.AtLeastOnce, ct);
        }

        private async Task OnMessageAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            var topic = args.ApplicationMessage.Topic;
            var rawJson = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);

            var parsed = MqttTopics.Parse(topic);
            if (parsed is null)
            {
                _logger.LogWarning("[MQTT-IN] Topic noto'g'ri formatda: {Topic}", topic);
                BotEnergyMetrics.RecordRejected("topic", "unknown");
                return;
            }

            var topicKind = parsed.Kind.ToString();
            BotEnergyMetrics.RecordReceived(topicKind);

            // Trace: qurilma yuborgan envelope'dagi traceparent bilan bog'lanadi (agar bor bo'lsa),
            // shunda "mobil buyruq berdi → qurilma javob qaytardi" zanjiri bitta trace'da ko'rinadi.
            using var activity = ObservabilityExtensions.ActivitySource.StartActivity(
                $"mqtt receive {topicKind}", ActivityKind.Consumer);
            activity?.SetTag("device.serial", parsed.SerialNumber);
            activity?.SetTag("mqtt.topic", topic);

            var stopwatch = Stopwatch.StartNew();

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
                    // LastSeenAt yangilash + offline→online edge bo'lsa DeviceStatusChanged{Online} chiqarish.
                    var deviceStatus = scope.ServiceProvider.GetRequiredService<IDeviceStatusService>();
                    await deviceStatus.MarkSeenAsync(parsed.SerialNumber);

                    BotEnergyMetrics.RecordHandled(context.Envelope?.Type ?? topicKind);
                }
                else
                {
                    // Pipeline device'ni topa olmadi yoki middleware zanjiri to'xtatdi —
                    // aniq sababni tegishli middleware o'zi yozadi.
                    activity?.SetStatus(ActivityStatusCode.Error, "pipeline stopped");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[MQTT-IN] Pipeline ishlatishda kutilmagan xato topic={Topic}",
                    topic);
                BotEnergyMetrics.RecordRejected("exception", topicKind);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            }
            finally
            {
                stopwatch.Stop();
                BotEnergyMetrics.MqttPipelineDuration.Record(
                    stopwatch.Elapsed.TotalMilliseconds,
                    new KeyValuePair<string, object?>("kind", topicKind));
            }
        }
    }
}
