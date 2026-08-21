using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using SessionApi.Mqtt.Abstractions;

namespace SessionApi.Mqtt.Handlers
{
    /// <summary>
    /// <c>device/{serial}/request</c>, <c>type=cash.session.cancel</c>.
    ///
    /// Mijoz kartani kiritib, lekin pul solmasdan fikridan qaytdi.
    ///
    /// Pul allaqachon solingan bo'lsa bekor qilib BO'LMAYDI — bill acceptor qabul qilingan
    /// kupyurani qaytara olmaydi, shuning uchun yagona to'g'ri yakun uni kartaga o'tkazish.
    /// Bunday holda server 409 qaytaradi va qurilma "kartaga tushirish" ni taklif qilishi kerak.
    /// </summary>
    [MqttHandler(MqttHandlerTypes.CashSessionCancel, MqttTopicKind.Request)]
    public sealed class CashSessionCancelHandler
        : MqttCommandHandler<CashSessionCancelHandler.Payload, CashSessionCancelHandler.Ack>
    {
        private readonly ICashTopUpService _cashService;
        private readonly ILogger<CashSessionCancelHandler> _logger;

        public CashSessionCancelHandler(
            ICashTopUpService cashService,
            ILogger<CashSessionCancelHandler> logger)
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

            var result = await _cashService.CancelAsync(
                context.SerialNumber, payload.CashSessionId.Value, context.CancellationToken);

            if (!result.IsSuccess)
            {
                return MqttResponseEnvelope.Fail<Ack>(
                    CashResultCodes.FromResult(result), result.ErrorObj!.ErrorMessage);
            }

            var dto = result.Result!;

            _logger.LogInformation(
                "[cash.session.cancel] serial={Serial} sessionId={SessionId}",
                context.SerialNumber, dto.CashSessionId);

            return MqttResponseEnvelope.Success(
                CashResultCodes.Success,
                dto.Message ?? "Sessiya bekor qilindi.",
                new Ack(dto.CashSessionId, dto.Status.ToString().ToLowerInvariant()));
        }

        public sealed class Payload
        {
            public long? CashSessionId { get; set; }
        }

        public sealed record Ack(long CashSessionId, string Status);
    }
}
