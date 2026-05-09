using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
    /// <summary>
    /// IPushNotificationService stub — faqat log yozadi.
    /// Production'da FcmPushNotificationService bilan almashtirilishi kerak.
    /// </summary>
    public sealed class LoggingPushNotificationService : IPushNotificationService
    {
        private readonly ILogger<LoggingPushNotificationService> _logger;

        public LoggingPushNotificationService(ILogger<LoggingPushNotificationService> logger)
        {
            _logger = logger;
        }

        public Task SendAsync(long userId, PushNotification notification, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "[PUSH] user={UserId} title=\"{Title}\" body=\"{Body}\" deepLink={DeepLink}",
                userId, notification.Title, notification.Body, notification.DeepLink);
            return Task.CompletedTask;
        }
    }
}
