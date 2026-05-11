using CommonConfiguration.Messaging;
using Domain.Dtos.Payment;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Messaging;
using Domain.Messaging.Commands;
using Domain.Messaging.Events;
using Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace UserApi.Messaging
{
    /// <summary>
    /// Qurilmadan kelgan QR to'lov so'rovini qayta ishlaydi.
    /// PaymentEventQueue: Qurilma → MQTT → DeviceApi → RabbitMQ → UserApi → IPaymentService.
    /// Natija PaymentCommandQueue orqali qaytariladi (DeviceApi MQTT'ga publish qiladi).
    /// </summary>
    public sealed class DevicePaymentEventConsumer : RabbitMqConsumerBase<DevicePaymentRequest>
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly RabbitMqPublisher _publisher;
        private readonly ILogger<DevicePaymentEventConsumer> _logger;

        public DevicePaymentEventConsumer(
            RabbitMqConnectionManager connectionManager,
            IServiceScopeFactory scopeFactory,
            RabbitMqPublisher publisher,
            ILogger<DevicePaymentEventConsumer> logger)
            : base(connectionManager, logger, QueueNames.PaymentEventQueue)
        {
            _scopeFactory = scopeFactory;
            _publisher = publisher;
            _logger = logger;
        }

        protected override async Task HandleMessageAsync(DevicePaymentRequest request)
        {
            _logger.LogInformation(
                "Device payment so'rovi: serial={Serial} amount={Amount} sessionToken=***",
                request.SerialNumber, request.Amount);

            using var scope = _scopeFactory.CreateScope();
            var sessionRepo = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
            var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
            var notifier = scope.ServiceProvider.GetRequiredService<ISessionNotifier>();

            // 1. SessionToken → userId
            var session = await sessionRepo.GetByTokenAsync(request.SessionToken);
            if (session is null)
            {
                _logger.LogWarning(
                    "Sessiya topilmadi (serial={Serial}). To'lov rad etildi.", request.SerialNumber);
                PublishResult(request, transactionId: 0, PaymentStatus.Failed, newBalance: null,
                    "Sessiya topilmadi yoki yopilgan.");
                return;
            }

            if (session.Status == SessionStatus.Closed)
            {
                _logger.LogWarning(
                    "Sessiya yopilgan (serial={Serial}, sessionId={SessionId}). To'lov rad etildi.",
                    request.SerialNumber, session.Id);
                PublishResult(request, transactionId: 0, PaymentStatus.Failed, newBalance: null,
                    "Sessiya allaqachon yopilgan.");
                return;
            }

            // 2. PaymentService.ProcessQrTopUpAsync (audit izi avtomatik yoziladi)
            // Device path → har doim foydalanuvchi shaxsiy balansi (org top-up faqat mobile orqali, qaror PR-rejada)
            var dto = new QrTopUpRequestDto
            {
                InitiatedByUserId = session.UserId,
                PayeeType = PaymentPayeeType.User,
                Amount = request.Amount,
                PaymeToken = request.PaymeToken,
                SessionId = session.Id,
                DeviceSerial = request.SerialNumber,
                IdempotencyKey = request.ClientRef // qurilmadan kelgan idempotency identifikator
            };

            var result = await paymentService.ProcessQrTopUpAsync(dto);

            // 3. Natijani qurilmaga qaytarish + SignalR orqali mobile'ga ham xabar
            if (result.IsSuccess)
            {
                var r = result.Result!;
                PublishResult(request, r.TransactionId, r.Status, r.NewBalance, r.ResultMessage);

                if (r.Status == PaymentStatus.Succeeded)
                {
                    await notifier.NotifyUserAsync(session.UserId, "PaymentSucceeded", new
                    {
                        transactionId = r.TransactionId,
                        payeeType = nameof(PaymentPayeeType.User),
                        amount = request.Amount,
                        newBalance = r.NewBalance,
                        source = "device"
                    });
                }
            }
            else
            {
                PublishResult(request, transactionId: 0, PaymentStatus.Failed, newBalance: null,
                    result.ErrorObj?.ErrorMessage ?? "To'lov bajarilmadi.");
            }
        }

        private void PublishResult(
            DevicePaymentRequest request,
            long transactionId,
            PaymentStatus status,
            decimal? newBalance,
            string? message)
        {
            _publisher.Publish(QueueNames.PaymentCommandQueue, new DevicePaymentResult
            {
                SerialNumber = request.SerialNumber,
                TransactionId = transactionId,
                Status = status,
                Amount = request.Amount,
                NewBalance = newBalance,
                Message = message,
                ClientRef = request.ClientRef
            });
        }
    }
}
