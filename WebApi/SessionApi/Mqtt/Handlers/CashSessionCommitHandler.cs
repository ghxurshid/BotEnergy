using Domain.Enums;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using SessionApi.Mqtt.Abstractions;

namespace SessionApi.Mqtt.Handlers
{
    /// <summary>
    /// <c>device/{serial}/request</c>, <c>type=cash.session.commit</c>.
    ///
    /// Mijoz "kartaga tushirilsin" tugmasini bosdi. Server yig'ilgan summani bankka yuboradi.
    ///
    /// Javob uch xil bo'lishi mumkin:
    ///  - <c>SUCCESS</c> — pul kartaga o'tdi, sessiya yopildi;
    ///  - <c>PAYOUT_PENDING</c> — bank javob bermadi, watcher qayta urinadi va yakuniy
    ///    natijani keyin <c>cash.session.result</c> bilan push qiladi;
    ///  - <c>PAYOUT_FAILED</c> — yakuniy xato, operator aralashuvi kerak.
    ///
    /// Pul allaqachon qurilma ichida, shuning uchun xato holatida ham sessiya yo'qolmaydi —
    /// u <c>PayoutFailed</c> bo'lib admin ro'yxatida qoladi.
    /// </summary>
    [MqttHandler(MqttHandlerTypes.CashSessionCommit, MqttTopicKind.Request)]
    public sealed class CashSessionCommitHandler
        : MqttCommandHandler<CashSessionCommitHandler.Payload, CashSessionCommitHandler.Ack>
    {
        private readonly ICashTopUpService _cashService;
        private readonly ILogger<CashSessionCommitHandler> _logger;

        public CashSessionCommitHandler(
            ICashTopUpService cashService,
            ILogger<CashSessionCommitHandler> logger)
        {
            _cashService = cashService;
            _logger = logger;
        }

        protected override async Task<MqttResponseEnvelope<Ack>> HandleAsync(Payload payload, MqttContext context)
        {
            if (payload.CashSessionId is null or 0)
            {
                return MqttResponseEnvelope.Fail<Ack>(
                    CashResultCodes.InvalidPayload, "cash_session_id majburiy.");
            }

            var result = await _cashService.CommitAsync(
                context.SerialNumber, payload.CashSessionId.Value, payload.ClientRef, context.CancellationToken);

            if (!result.IsSuccess)
            {
                return MqttResponseEnvelope.Fail<Ack>(
                    CashResultCodes.FromHttpCode(result.ErrorObj!.Code), result.ErrorObj.ErrorMessage);
            }

            var dto = result.Result!;
            var ack = new Ack(
                dto.CashSessionId,
                dto.Status.ToString().ToLowerInvariant(),
                dto.Amount,
                dto.PayoutReference,
                dto.DeviceCashBalance);

            // Holat qurilma uchun kodga aylantiriladi: u ekranda nima ko'rsatishni shundan biladi.
            var code = dto.Status switch
            {
                CashSessionStatus.Completed => CashResultCodes.Success,
                CashSessionStatus.PayoutFailed when dto.RetryScheduled => CashResultCodes.PayoutPending,
                CashSessionStatus.PayoutFailed => CashResultCodes.PayoutFailed,
                _ => CashResultCodes.Success
            };

            _logger.LogInformation(
                "[cash.session.commit] serial={Serial} sessionId={SessionId} status={Status} code={Code}",
                context.SerialNumber, dto.CashSessionId, dto.Status, code);

            return code == CashResultCodes.Success
                ? MqttResponseEnvelope.Success(code, dto.Message ?? "Bajarildi.", ack)
                : MqttResponseEnvelope.Fail<Ack>(code, dto.Message ?? "Xatolik.");
        }

        public sealed class Payload
        {
            public long? CashSessionId { get; set; }

            /// <summary>Idempotentlik kaliti — takroriy commit ikkinchi o'tkazma yasamaydi.</summary>
            public string? ClientRef { get; set; }
        }

        public sealed record Ack(
            long CashSessionId,
            string Status,
            decimal Amount,
            string? PayoutRef,
            decimal? DeviceCashBalance);
    }
}
