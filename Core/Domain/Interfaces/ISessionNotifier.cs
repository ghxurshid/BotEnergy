namespace Domain.Interfaces
{
    /// <summary>
    /// Sessiya hodisalarini real-time klientlarga yetkazish uchun abstraktsiya.
    /// Implementatsiya SignalR, WebSocket yoki boshqa transport bo'lishi mumkin.
    /// </summary>
    public interface ISessionNotifier
    {
        Task NotifyDeviceConnectedAsync(string sessionToken, object payload);
        Task NotifyProgressUpdateAsync(string sessionToken, object payload);
        Task NotifySessionCompletedAsync(string sessionToken, object payload);
        Task NotifySessionClosedAsync(string sessionToken, object payload);
    }
}
