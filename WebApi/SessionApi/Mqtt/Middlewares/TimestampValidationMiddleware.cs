using Microsoft.Extensions.Logging;
using SessionApi.Mqtt.Abstractions;

namespace SessionApi.Mqtt.Middlewares
{
    /// <summary>
    /// Envelope.Timestamp ni hozirgi vaqt bilan solishtiradi.
    /// Rad etiladi: timestamp 60 sekunddan ko'p eski yoki kelajakda (clock skew).
    /// </summary>
    public sealed class TimestampValidationMiddleware : IMqttMiddleware
    {
        private const int MaxAgeSeconds = 60;
        private const int MaxFutureSeconds = 5; // kichik clock skew tolerance

        private readonly ILogger<TimestampValidationMiddleware> _logger;

        public TimestampValidationMiddleware(ILogger<TimestampValidationMiddleware> logger) => _logger = logger;

        public Task InvokeAsync(MqttContext context, MqttNext next)
        {
            if (context.Envelope is null) return Task.CompletedTask;

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var ts = context.Envelope.Timestamp;
            var age = now - ts;

            if (age > MaxAgeSeconds)
            {
                _logger.LogWarning(
                    "[MQTT-IN] Timestamp juda eski age={Age}s id={Id} serial={Serial}",
                    age, context.Envelope.Id, context.SerialNumber);
                return Task.CompletedTask;
            }

            if (age < -MaxFutureSeconds)
            {
                _logger.LogWarning(
                    "[MQTT-IN] Timestamp kelajakdan skew={Skew}s id={Id} serial={Serial}",
                    -age, context.Envelope.Id, context.SerialNumber);
                return Task.CompletedTask;
            }

            return next();
        }
    }
}
