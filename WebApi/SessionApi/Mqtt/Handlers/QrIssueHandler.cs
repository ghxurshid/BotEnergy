using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using SessionApi.Mqtt.Abstractions;

namespace SessionApi.Mqtt.Handlers
{
    /// <summary>
    /// <c>device/{serial}/request</c>, <c>type=qr.issue</c> — kolonka ekranida
    /// ko'rsatish uchun bir martalik QR kod so'rash.
    ///
    /// Qurilma bo'sh (idle) turganda ekranda QR chizadi. Kod qisqa muddatli va
    /// faqat shu qurilmaga tegishli: mijoz uni skanerlab sessiya ochgach kod
    /// kuyadi, qurilma esa <c>session.attached</c> xabarini oladi.
    ///
    /// Qurilma kodni muddati tugashidan oldin (masalan 10 soniya qolganda)
    /// qayta so'rashi kerak — server har so'rovda yangi kod beradi va
    /// avvalgisini bekor qiladi.
    /// </summary>
    [MqttHandler(MqttHandlerTypes.QrIssue, MqttTopicKind.Request)]
    public sealed class QrIssueHandler : MqttCommandHandler<QrIssueHandler.Payload, QrIssueHandler.AckData>
    {
        private readonly ISessionService _sessionService;
        private readonly ILogger<QrIssueHandler> _logger;

        public QrIssueHandler(ISessionService sessionService, ILogger<QrIssueHandler> logger)
        {
            _sessionService = sessionService;
            _logger = logger;
        }

        protected override async Task<MqttResponseEnvelope<AckData>> HandleAsync(Payload payload, MqttContext context)
        {
            var result = await _sessionService.IssueDeviceQrAsync(context.SerialNumber, payload.TtlSeconds);

            if (!result.IsSuccess || result.Result is null)
            {
                var code = result.ErrorObj?.Reason ?? MqttResultCodes.InternalError;
                var message = result.ErrorObj?.ErrorMessage ?? "QR kod berilmadi.";

                _logger.LogWarning("[qr.issue] Rad etildi serial={Serial}: {Code} {Message}",
                    context.SerialNumber, code, message);

                return MqttResponseEnvelope.Fail<AckData>(code, message);
            }

            var qr = result.Result;

            _logger.LogInformation("[qr.issue] OK serial={Serial} ttl={Ttl}s", context.SerialNumber, qr.TtlSeconds);

            return MqttResponseEnvelope.Success(
                MqttResultCodes.Success,
                "QR kod berildi.",
                new AckData(qr.Payload, qr.TtlSeconds, new DateTimeOffset(qr.ExpiresAt).ToUnixTimeSeconds()));
        }

        public sealed class Payload
        {
            /// <summary>Ixtiyoriy: kerakli amal muddati (sekund). Server 600 s bilan cheklaydi.</summary>
            public int? TtlSeconds { get; set; }
        }

        /// <summary>
        /// <c>qr_payload</c> — QR ichiga aynan shu matn yoziladi.
        /// </summary>
        public sealed record AckData(string QrPayload, int TtlSeconds, long ExpiresAt);
    }
}
