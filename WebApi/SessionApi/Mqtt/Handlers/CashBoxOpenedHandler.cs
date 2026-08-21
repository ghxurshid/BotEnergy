using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using SessionApi.Mqtt.Abstractions;

namespace SessionApi.Mqtt.Handlers
{
    /// <summary>
    /// <c>device/{serial}/event</c>, <c>type=cash.box.opened</c>.
    ///
    /// Qurilma inkassator so'roviga ko'ra naqd boxni ochganini tasdiqlaydi.
    /// Event (fire-and-forget) — javob kutilmaydi, chunki inkassator natijani ilovada
    /// ko'radi, qurilma ekranida emas.
    ///
    /// Qoldiq bu yerda NOLGA TUSHMAYDI: box ochilgani pulning olinganini bildirmaydi.
    /// Nolga tushirish faqat inkassator ilovada tasdiqlaganda bo'ladi.
    /// </summary>
    [MqttHandler(MqttHandlerTypes.CashBoxOpened, MqttTopicKind.Event)]
    public sealed class CashBoxOpenedHandler : MqttEventHandler<CashBoxOpenedHandler.Payload>
    {
        private readonly IIncassationService _incassation;
        private readonly ILogger<CashBoxOpenedHandler> _logger;

        public CashBoxOpenedHandler(
            IIncassationService incassation,
            ILogger<CashBoxOpenedHandler> logger)
        {
            _incassation = incassation;
            _logger = logger;
        }

        protected override async Task HandleAsync(Payload payload, MqttContext context)
        {
            if (payload.CollectionId is null or 0)
            {
                _logger.LogWarning(
                    "[cash.box.opened] collection_id yo'q serial={Serial}", context.SerialNumber);
                return;
            }

            await _incassation.MarkBoxOpenedAsync(
                context.SerialNumber, payload.CollectionId.Value, context.CancellationToken);
        }

        public sealed class Payload
        {
            public long? CollectionId { get; set; }
        }
    }
}
