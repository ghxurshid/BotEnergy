namespace Domain.Interfaces
{
    /// <summary>
    /// Sessiya va process hodisalarini real-time klientlarga yetkazish uchun abstraktsiya.
    /// Implementatsiya SignalR, WebSocket yoki boshqa transport bo'lishi mumkin.
    ///
    /// Group sxemasi:
    ///   - sessionToken — sessiyaga ulangan barcha klientlar uchun (planshet+telefon)
    ///   - "user:{userId}" — foydalanuvchining hamma device lari uchun (sessionToken bilmasdan ham keladi)
    /// </summary>
    public interface ISessionNotifier
    {
        Task NotifyDeviceConnectedAsync(string sessionToken, object payload);
        Task NotifySessionUpdatedAsync(string sessionToken, object payload);
        Task NotifySessionClosedAsync(string sessionToken, object payload);

        Task NotifyProcessStartedAsync(string sessionToken, object payload);
        Task NotifyProcessUpdatedAsync(string sessionToken, object payload);
        Task NotifyProcessEndedAsync(string sessionToken, object payload);

        /// <summary>
        /// Foydalanuvchi guruhiga (user:{userId}) ixtiyoriy event yuborish.
        /// Klient SignalR hub'ga ulanishi bilan avtomatik shu group'ga qo'shiladi.
        /// </summary>
        Task NotifyUserAsync(long userId, string eventName, object payload);
    }
}
