using Microsoft.Extensions.Logging;
using SessionApi.Mqtt.Abstractions;

namespace SessionApi.Mqtt.Middlewares
{
    /// <summary>
    /// Pipeline boshida — inbound xabarni log qiladi va keyingi middleware'lardan kelgan
    /// xatolarni tutib log'ga yozadi.
    /// </summary>
    public sealed class LoggingMiddleware : IMqttMiddleware
    {
        private readonly ILogger<LoggingMiddleware> _logger;

        public LoggingMiddleware(ILogger<LoggingMiddleware> logger) => _logger = logger;

        public async Task InvokeAsync(MqttContext context, MqttNext next)
        {
            _logger.LogInformation(
                "[MQTT-IN] topic={Topic} serial={Serial} kind={Kind} payloadLen={Len}",
                context.Topic, context.SerialNumber, context.TopicKind, context.RawJson.Length);

            try
            {
                await next();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[MQTT-IN] Pipeline xatosi topic={Topic} serial={Serial}",
                    context.Topic, context.SerialNumber);
            }
        }
    }
}
