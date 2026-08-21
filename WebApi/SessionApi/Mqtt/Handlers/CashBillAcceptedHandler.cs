using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using SessionApi.Mqtt.Abstractions;

namespace SessionApi.Mqtt.Handlers
{
    /// <summary>
    /// <c>device/{serial}/request</c>, <c>type=cash.bill.accepted</c>.
    ///
    /// Bill acceptor har bir kupyurani qabul qilganda yuboriladi. Event emas, REQUEST:
    /// qurilma ekranida SERVERDAGI jami summa ko'rsatilishi kerak, ya'ni javob talab qilinadi.
    ///
    /// <c>bill_seq</c> — qurilmadagi kupyura tartib raqami. Xabar qayta yuborilsa
    /// (qurilma javobni olmay qolsa) summa ikki marta oshmaydi: <c>Added=false</c> qaytadi,
    /// jami esa haqiqiy qiymatni ko'rsatadi.
    /// </summary>
    [MqttHandler(MqttHandlerTypes.CashBillAccepted, MqttTopicKind.Request)]
    public sealed class CashBillAcceptedHandler
        : MqttCommandHandler<CashBillAcceptedHandler.Payload, CashBillAcceptedHandler.Ack>
    {
        private readonly ICashTopUpService _cashService;
        private readonly ILogger<CashBillAcceptedHandler> _logger;

        public CashBillAcceptedHandler(
            ICashTopUpService cashService,
            ILogger<CashBillAcceptedHandler> logger)
        {
            _cashService = cashService;
            _logger = logger;
        }

        protected override async Task<MqttResponseEnvelope<Ack>> HandleAsync(Payload payload, MqttContext context)
        {
            if (payload.CashSessionId is null or 0 || payload.Denomination <= 0 || payload.BillSeq is null or 0)
            {
                return MqttResponseEnvelope.Fail<Ack>(
                    CashResultCodes.InvalidPayload,
                    "cash_session_id, denomination va bill_seq majburiy (noldan katta).");
            }

            var result = await _cashService.AddBillAsync(
                context.SerialNumber,
                payload.CashSessionId.Value,
                payload.Denomination,
                payload.BillSeq.Value,
                context.CancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "[cash.bill.accepted] Rad etildi serial={Serial} sessionId={SessionId} seq={Seq} sabab={Reason}",
                    context.SerialNumber, payload.CashSessionId, payload.BillSeq, result.ErrorObj!.Reason);

                return MqttResponseEnvelope.Fail<Ack>(
                    CashResultCodes.FromResult(result), result.ErrorObj.ErrorMessage);
            }

            var total = result.Result!;

            return MqttResponseEnvelope.Success(
                CashResultCodes.Success,
                total.Added ? "Kupyura qabul qilindi." : "Bu kupyura allaqachon hisobga olingan.",
                new Ack(total.CashSessionId, total.AcceptedTotal, total.BillCount, total.Added));
        }

        public sealed class Payload
        {
            public long? CashSessionId { get; set; }
            public decimal Denomination { get; set; }
            public int? BillSeq { get; set; }
        }

        public sealed record Ack(long CashSessionId, decimal AcceptedTotal, int BillCount, bool Added);
    }
}
