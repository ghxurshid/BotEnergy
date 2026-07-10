using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using SessionApi.Mqtt.Abstractions;

namespace SessionApi.Mqtt.Middlewares
{
    /// <summary>
    /// Monotonic id replay protection. Device'ning outbound counter'i avvalgi qabul qilingandan
    /// katta bo'lishi shart — istisnosiz (<c>session.connect</c> ham). Counter'lar hech qachon
    /// avtomatik reset qilinmaydi; qurilma EEPROM'i qayta flash qilinsa serverdagi counter
    /// faqat expert-rejim admin endpoint'i orqali 0'ga tushiriladi.
    /// </summary>
    public sealed class ReplayValidationMiddleware : IMqttMiddleware
    {
        private readonly IMqttMessageIdStore _idStore;
        private readonly ILogger<ReplayValidationMiddleware> _logger;

        public ReplayValidationMiddleware(
            IMqttMessageIdStore idStore,
            ILogger<ReplayValidationMiddleware> logger)
        {
            _idStore = idStore;
            _logger = logger;
        }

        public async Task InvokeAsync(MqttContext context, MqttNext next)
        {
            if (context.Envelope is null) return;

            // Response topic: id echo qilingan (server outbound counter'iga tegishli), monotonic
            // tekshiruv qo'llanmaydi. HMAC + outstanding-request matching yetarli.
            if (context.TopicKind == MqttTopicKind.Response)
            {
                await next();
                return;
            }

            if (!await _idStore.TryAcceptInboundIdAsync(context.SerialNumber, context.Envelope.Id))
            {
                _logger.LogWarning(
                    "[MQTT-IN] Replay rad etildi id={Id} type={Type} serial={Serial}",
                    context.Envelope.Id, context.Envelope.Type, context.SerialNumber);
                return;
            }

            await next();
        }
    }
}
