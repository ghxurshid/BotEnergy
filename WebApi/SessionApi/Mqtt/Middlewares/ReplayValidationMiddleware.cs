using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using SessionApi.Mqtt.Abstractions;

namespace SessionApi.Mqtt.Middlewares
{
    /// <summary>
    /// Monotonic id replay protection. Device'ning outbound counter'i avvalgi qabul qilingandan
    /// katta bo'lishi shart. Connect uchun (<c>type=session.connect</c>) skip qilinadi — handler
    /// muvaffaqiyatdan keyin counter'larni reset qiladi.
    /// </summary>
    public sealed class ReplayValidationMiddleware : IMqttMiddleware
    {
        public const string SessionConnectType = "session.connect";

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

            // Connect — counter'lar reset/flash dan keyin mos kelmasligi mumkin. Skip.
            if (context.Envelope.Type == SessionConnectType)
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
