namespace Domain.Interfaces
{
    /// <summary>
    /// Mobil push notification yuborish abstraktsiyasi (FCM/APNs).
    /// SignalR offline klientga yetib bormaydi — sessiya timeout, qurilma uzilishi
    /// va shu kabi muhim eventlar uchun push kerak.
    ///
    /// V1 implementatsiyasi: stub (faqat log). FCM/APNs ulanishi keyingi sprintda.
    /// </summary>
    public interface IPushNotificationService
    {
        Task SendAsync(long userId, PushNotification notification, CancellationToken cancellationToken = default);
    }

    public sealed class PushNotification
    {
        public required string Title { get; init; }
        public required string Body { get; init; }
        public string? DeepLink { get; init; }
        public IReadOnlyDictionary<string, string>? Data { get; init; }
    }
}
