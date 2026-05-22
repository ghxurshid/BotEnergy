using Microsoft.Extensions.Logging;
using SessionApi.Mqtt.Abstractions;

namespace SessionApi.Mqtt.Middlewares
{
    /// <summary>
    /// Envelope HMAC ni <see cref="MqttContext.Device"/>.SecretKey bilan tekshiradi.
    /// <see cref="DeviceAuthMiddleware"/> dan keyin ishlashi shart.
    /// </summary>
    public sealed class HmacValidationMiddleware : IMqttMiddleware
    {
        private readonly ILogger<HmacValidationMiddleware> _logger;

        public HmacValidationMiddleware(ILogger<HmacValidationMiddleware> logger) => _logger = logger;

        public Task InvokeAsync(MqttContext context, MqttNext next)
        {
            if (context.Envelope is null || context.Device is null)
            {
                _logger.LogWarning("[MQTT-IN] HMAC tekshirildi — envelope yoki device null serial={Serial}",
                    context.SerialNumber);
                return Task.CompletedTask;
            }

            if (!MqttEnvelopeSerializer.VerifyHmac(context.Envelope, context.Device.SecretKey))
            {
                _logger.LogWarning(
                    "[MQTT-IN] HMAC mos kelmadi id={Id} type={Type} serial={Serial}",
                    context.Envelope.Id, context.Envelope.Type, context.SerialNumber);
                return Task.CompletedTask;
            }

            return next();
        }
    }
}
