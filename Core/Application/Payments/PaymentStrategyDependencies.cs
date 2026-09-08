using Domain.Interfaces;
using Domain.Options;
using Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Application.Payments
{
    /// <summary>
    /// Barcha to'lov strategiyalari uchun umumiy bog'liqliklar to'plami — har bir strategiya
    /// konstruktorida o'n dona repo sanab o'tmasligi uchun. Faqat <c>PaymentStrategyBase</c>
    /// ishlatadi; strategiyalar o'zining provider-klientini alohida oladi.
    /// SessionApi-only (ISessionNotifier / IDeviceCommandPublisher shu jarayonda).
    /// </summary>
    public sealed class PaymentStrategyDependencies
    {
        public IPaymentIntentRepository Intents { get; }
        public IPaymentSessionRepository PaymentSessions { get; }
        public ISessionRepository Sessions { get; }
        public IDeviceRepository Devices { get; }
        public IProductProcessRepository Processes { get; }
        public ISessionNotifier Notifier { get; }
        public IDeviceCommandPublisher Commands { get; }
        public IPushNotificationService Push { get; }
        public ITransactionRunner Tx { get; }
        public PaymentOptions Options { get; }

        public PaymentStrategyDependencies(
            IPaymentIntentRepository intents,
            IPaymentSessionRepository paymentSessions,
            ISessionRepository sessions,
            IDeviceRepository devices,
            IProductProcessRepository processes,
            ISessionNotifier notifier,
            IDeviceCommandPublisher commands,
            IPushNotificationService push,
            ITransactionRunner tx,
            IOptions<PaymentOptions> options)
        {
            Intents = intents;
            PaymentSessions = paymentSessions;
            Sessions = sessions;
            Devices = devices;
            Processes = processes;
            Notifier = notifier;
            Commands = commands;
            Push = push;
            Tx = tx;
            Options = options.Value;
        }
    }
}
