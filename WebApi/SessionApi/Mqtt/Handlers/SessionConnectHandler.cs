using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using SessionApi.Mqtt.Abstractions;
using SessionApi.Services;

namespace SessionApi.Mqtt.Handlers
{
    /// <summary>
    /// <c>device/{serial}/request</c>, <c>type=session.connect</c> — QR ulanish oqimi.
    /// Mobil ilova pending sessiya yaratadi, qurilma QR ni o'qib serverga bu request yuboradi.
    /// Server kalitlarni solishtirib sessiyani DB'da yaratadi va connect natijasini response sifatida qaytaradi.
    ///
    /// <para>Connect muvaffaqiyatli bo'lganda <see cref="IMqttMessageIdStore.ResetAsync"/> chaqiriladi —
    /// qurilma reset/flash bo'lganda counter'lar mos kelishini ta'minlash uchun.</para>
    /// </summary>
    [MqttHandler(MqttHandlerTypes.SessionConnect, MqttTopicKind.Request)]
    public sealed class SessionConnectHandler : MqttCommandHandler<SessionConnectHandler.Payload, ConnectAckData>
    {
        private readonly IDeviceSessionService _sessionService;
        private readonly IMqttMessageIdStore _idStore;
        private readonly ILogger<SessionConnectHandler> _logger;

        public SessionConnectHandler(
            IDeviceSessionService sessionService,
            IMqttMessageIdStore idStore,
            ILogger<SessionConnectHandler> logger)
        {
            _sessionService = sessionService;
            _idStore = idStore;
            _logger = logger;
        }

        protected override async Task<MqttResponseEnvelope<ConnectAckData>> HandleAsync(Payload payload, MqttContext context)
        {
            _logger.LogInformation(
                "[session.connect] serial={Serial} userId={UserId} tokenLen={Len}",
                context.SerialNumber, payload.UserId, payload.SessionToken?.Length ?? 0);

            if (payload.UserId is null or 0 || string.IsNullOrEmpty(payload.SessionToken))
            {
                return MqttResponseEnvelope.Fail<ConnectAckData>(
                    ConnectResultCodes.InvalidPayload,
                    "user_id (>0) va session_token (bo'sh emas) majburiy.");
            }

            var result = await _sessionService.TryConnectAsync(
                context.SerialNumber, payload.UserId.Value, payload.SessionToken, context.CancellationToken);

            if (!result.Success)
            {
                return MqttResponseEnvelope.Fail<ConnectAckData>(result.Code, result.Message);
            }

            // Counter'larni reset qilamiz va shu connect request idini inbound counter'iga yozamiz.
            // Replay middleware connect uchun skip qildi — endi keyingi xabarlar id > connectId bo'lishi kerak.
            await _idStore.ResetAsync(context.SerialNumber);
            await _idStore.TryAcceptInboundIdAsync(context.SerialNumber, context.Envelope!.Id);

            _logger.LogInformation(
                "[session.connect] OK serial={Serial} sessionId={SessionId} — counter'lar reset id={Id} dan",
                context.SerialNumber, result.SessionId, context.Envelope.Id);

            return MqttResponseEnvelope.Success(
                result.Code, result.Message,
                new ConnectAckData(result.SessionId!.Value));
        }

        public sealed class Payload
        {
            public long? UserId { get; set; }
            public string? SessionToken { get; set; }
        }
    }

    /// <summary>session.connect muvaffaqiyatli response data.</summary>
    public sealed record ConnectAckData(long SessionId);
}
