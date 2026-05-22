using Microsoft.Extensions.Logging;
using SessionApi.Mqtt.Abstractions;

namespace SessionApi.Mqtt.Middlewares
{
    /// <summary>
    /// Raw JSON ni <see cref="MqttEnvelope"/> ga parse qiladi. HMAC tekshirilmaydi —
    /// bu <c>HmacValidationMiddleware</c> ning vazifasi.
    /// </summary>
    public sealed class DeserializeMiddleware : IMqttMiddleware
    {
        private readonly ILogger<DeserializeMiddleware> _logger;

        public DeserializeMiddleware(ILogger<DeserializeMiddleware> logger) => _logger = logger;

        public Task InvokeAsync(MqttContext context, MqttNext next)
        {
            if (!MqttEnvelopeSerializer.TryParse(context.RawJson, out var envelope, out var error))
            {
                _logger.LogWarning(
                    "[MQTT-IN] Envelope parse rad etildi reason={Reason} serial={Serial}",
                    error, context.SerialNumber);
                return Task.CompletedTask; // pipeline to'xtaydi — next() chaqirilmaydi
            }

            context.Envelope = envelope;
            return next();
        }
    }
}
