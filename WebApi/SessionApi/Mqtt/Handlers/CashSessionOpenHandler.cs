using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using SessionApi.Mqtt.Abstractions;

namespace SessionApi.Mqtt.Handlers
{
    /// <summary>
    /// <c>device/{serial}/request</c>, <c>type=cash.session.open</c>.
    ///
    /// Mijoz kolonka ekranida "Kartani to'ldirish" ni tanlab 16 xonali karta raqamini kiritadi.
    /// Server kartani bankda tekshiradi va naqd qabul qilish sessiyasini ochadi.
    ///
    /// <b>To'liq PAN faqat shu xabarda keladi</b> va servisdan nariga o'tmaydi: bazaga
    /// maska va bank tokeni yoziladi, logga esa hech qachon to'liq raqam tushmaydi.
    /// </summary>
    [MqttHandler(MqttHandlerTypes.CashSessionOpen, MqttTopicKind.Request)]
    public sealed class CashSessionOpenHandler
        : MqttCommandHandler<CashSessionOpenHandler.Payload, CashSessionOpenHandler.Ack>
    {
        private readonly ICashTopUpService _cashService;
        private readonly ILogger<CashSessionOpenHandler> _logger;

        public CashSessionOpenHandler(
            ICashTopUpService cashService,
            ILogger<CashSessionOpenHandler> logger)
        {
            _cashService = cashService;
            _logger = logger;
        }

        protected override async Task<MqttResponseEnvelope<Ack>> HandleAsync(Payload payload, MqttContext context)
        {
            if (string.IsNullOrWhiteSpace(payload.CardPan))
            {
                return MqttResponseEnvelope.Fail<Ack>(
                    CashResultCodes.InvalidPayload, "card_pan majburiy.");
            }

            var result = await _cashService.OpenSessionAsync(
                context.SerialNumber, payload.CardPan, context.CancellationToken);

            if (!result.IsSuccess)
            {
                // Kod to'sqinlik omilidan olinadi — qurilma qaysi ekranni ko'rsatishini
                // HTTP statusdan taxmin qilmaydi.
                return MqttResponseEnvelope.Fail<Ack>(
                    CashResultCodes.FromResult(result), result.ErrorObj!.ErrorMessage);
            }

            var opened = result.Result!;

            _logger.LogInformation(
                "[cash.session.open] OK serial={Serial} sessionId={SessionId} card={Masked}",
                context.SerialNumber, opened.CashSessionId, opened.CardMasked);

            return MqttResponseEnvelope.Success(
                CashResultCodes.Success,
                "Karta tasdiqlandi. Naqd pul qabul qilishingiz mumkin.",
                new Ack(opened.CashSessionId, opened.CardMasked));
        }

        public sealed class Payload
        {
            /// <summary>16 xonali karta raqami. Saqlanmaydi — faqat bank tekshiruvi uchun.</summary>
            public string? CardPan { get; set; }
        }

        public sealed record Ack(long CashSessionId, string CardMasked);
    }
}
