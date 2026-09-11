using CommonConfiguration.Observability;
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
                // Sababsiz "HMAC mos kelmadi" dalada foydasiz: kalit eskirganmi yoki payload
                // matni farq qilyaptimi — ikkisi butunlay boshqa ishni talab qiladi.
                var cause = MqttEnvelopeSerializer.DiagnoseMismatch(context.Envelope, context.Device.SecretKey);

                _logger.LogWarning(
                    "[MQTT-IN] HMAC mos kelmadi id={Id} type={Type} serial={Serial} sabab={Cause} " +
                    "serverKeyFp={KeyFp} payloadLen={PayloadLen} hmacGot={HmacGot} hmacExp={HmacExp}",
                    context.Envelope.Id, context.Envelope.Type, context.SerialNumber, cause,
                    MqttEnvelopeSerializer.KeyFingerprint(context.Device.SecretKey),
                    context.Envelope.PayloadJson.Length,
                    MqttEnvelopeSerializer.ShortHmac(context.Envelope.Hmac),
                    MqttEnvelopeSerializer.ShortHmac(MqttEnvelopeSerializer.ComputeHmac(
                        context.Envelope.Id, context.Envelope.Type, context.Envelope.Timestamp,
                        context.Envelope.PayloadJson, context.Device.SecretKey)));

                BotEnergyMetrics.RecordRejected($"hmac_{cause}", context.TopicKind.ToString());
                return Task.CompletedTask;
            }

            return next();
        }
    }
}
