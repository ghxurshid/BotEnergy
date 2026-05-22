using Domain.Dtos.Payment;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Repositories;
using Microsoft.Extensions.Logging;
using SessionApi.Mqtt.Abstractions;

namespace SessionApi.Mqtt.Handlers
{
    /// <summary>
    /// <c>device/{serial}/event</c>, <c>type=payment.qr</c>.
    /// Qurilma Payme QR ni o'qib serverga to'lov so'rovi yuboradi.
    /// Handler ichida <see cref="IPaymentService.ProcessQrTopUpAsync"/> chaqiriladi,
    /// natija MQTT response sifatida qurilmaga + SignalR orqali mobilega push qilinadi
    /// (oraliq RabbitMQ queue yo'q — idempotency <c>ClientRef</c> bilan ta'minlanadi).
    /// </summary>
    [MqttHandler(MqttHandlerTypes.PaymentQr, MqttTopicKind.Event)]
    public sealed class PaymentQrHandler : MqttEventHandler<PaymentQrHandler.Payload>
    {
        private readonly ISessionRepository _sessionRepo;
        private readonly IPaymentService _paymentService;
        private readonly ISessionNotifier _notifier;
        private readonly IMqttPublisher _publisher;
        private readonly ILogger<PaymentQrHandler> _logger;

        public PaymentQrHandler(
            ISessionRepository sessionRepo,
            IPaymentService paymentService,
            ISessionNotifier notifier,
            IMqttPublisher publisher,
            ILogger<PaymentQrHandler> logger)
        {
            _sessionRepo = sessionRepo;
            _paymentService = paymentService;
            _notifier = notifier;
            _publisher = publisher;
            _logger = logger;
        }

        protected override async Task HandleAsync(Payload payload, MqttContext context)
        {
            if (string.IsNullOrEmpty(payload.SessionToken) ||
                string.IsNullOrEmpty(payload.PaymeToken) ||
                payload.Amount <= 0)
            {
                _logger.LogWarning(
                    "[payment.qr] Yaroqsiz payload serial={Serial} amount={Amount}",
                    context.SerialNumber, payload.Amount);
                return;
            }

            // 1. SessionToken → userId
            var session = await _sessionRepo.GetByTokenAsync(payload.SessionToken);
            if (session is null || session.Status == SessionStatus.Closed)
            {
                _logger.LogWarning(
                    "[payment.qr] Sessiya yo'q yoki yopilgan serial={Serial}", context.SerialNumber);
                await PublishResultAsync(context.SerialNumber, transactionId: 0, PaymentStatus.Failed,
                    newBalance: null, message: "Sessiya topilmadi yoki yopilgan.",
                    amount: payload.Amount, clientRef: payload.ClientRef, context.CancellationToken);
                return;
            }

            // 2. PaymentService.ProcessQrTopUpAsync (idempotency clientRef orqali)
            var dto = new QrTopUpRequestDto
            {
                InitiatedByUserId = session.UserId,
                PayeeType = PaymentPayeeType.User,
                Amount = payload.Amount,
                PaymeToken = payload.PaymeToken,
                SessionId = session.Id,
                DeviceSerial = context.SerialNumber,
                IdempotencyKey = payload.ClientRef
            };

            var result = await _paymentService.ProcessQrTopUpAsync(dto, context.CancellationToken);

            // 3. Natija qurilmaga MQTT request + mobilega SignalR (success bo'lsa)
            if (result.IsSuccess)
            {
                var r = result.Result!;
                await PublishResultAsync(context.SerialNumber, r.TransactionId, r.Status, r.NewBalance,
                    r.ResultMessage, payload.Amount, payload.ClientRef, context.CancellationToken);

                if (r.Status == PaymentStatus.Succeeded)
                {
                    await _notifier.NotifyUserAsync(session.UserId, "PaymentSucceeded", new
                    {
                        transactionId = r.TransactionId,
                        payeeType = nameof(PaymentPayeeType.User),
                        amount = payload.Amount,
                        newBalance = r.NewBalance,
                        source = "device"
                    });
                }
            }
            else
            {
                await PublishResultAsync(context.SerialNumber, transactionId: 0, PaymentStatus.Failed,
                    newBalance: null, message: result.ErrorObj?.ErrorMessage ?? "To'lov bajarilmadi.",
                    amount: payload.Amount, clientRef: payload.ClientRef, context.CancellationToken);
            }
        }

        private Task PublishResultAsync(
            string serial, long transactionId, PaymentStatus status, decimal? newBalance,
            string? message, decimal amount, string? clientRef, CancellationToken ct)
            => _publisher.PublishRequestAsync(serial, MqttHandlerTypes.PaymentResult, new
            {
                transaction_id = transactionId,
                status = status.ToString().ToLowerInvariant(),
                amount,
                new_balance = newBalance,
                message,
                client_ref = clientRef
            }, ct);

        public sealed class Payload
        {
            public string SessionToken { get; set; } = string.Empty;
            public decimal Amount { get; set; }
            public string PaymeToken { get; set; } = string.Empty;
            public string? ClientRef { get; set; }
        }
    }
}
