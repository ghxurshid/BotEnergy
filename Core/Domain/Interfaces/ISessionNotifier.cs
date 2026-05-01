namespace Domain.Interfaces
{
    /// <summary>
    /// Sessiya va process hodisalarini real-time klientlarga yetkazish uchun abstraktsiya.
    /// Implementatsiya SignalR, WebSocket yoki boshqa transport bo'lishi mumkin.
    /// Group key — har doim sessionToken.
    /// </summary>
    public interface ISessionNotifier
    {
        Task NotifyDeviceConnectedAsync(string sessionToken, object payload);
        Task NotifySessionUpdatedAsync(string sessionToken, object payload);
        Task NotifySessionClosedAsync(string sessionToken, object payload);

        Task NotifyProcessStartedAsync(string sessionToken, object payload);
        Task NotifyProcessUpdatedAsync(string sessionToken, object payload);
        Task NotifyProcessEndedAsync(string sessionToken, object payload);
    }
}
