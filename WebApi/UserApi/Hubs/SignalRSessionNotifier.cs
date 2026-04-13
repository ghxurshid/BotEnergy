using Domain.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace UserApi.Hubs
{
    /// <summary>
    /// ISessionNotifier ning SignalR implementatsiyasi.
    /// SessionService bu interfeys orqali klientlarga xabar yuboradi —
    /// qaysi transport ishlatilishini bilmaydi.
    /// </summary>
    public sealed class SignalRSessionNotifier : ISessionNotifier
    {
        private readonly IHubContext<SessionHub> _hubContext;

        public SignalRSessionNotifier(IHubContext<SessionHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public Task NotifyDeviceConnectedAsync(string sessionToken, object payload)
            => _hubContext.Clients.Group(sessionToken).SendAsync("DeviceConnected", payload);

        public Task NotifyProgressUpdateAsync(string sessionToken, object payload)
            => _hubContext.Clients.Group(sessionToken).SendAsync("ProgressUpdate", payload);

        public Task NotifySessionCompletedAsync(string sessionToken, object payload)
            => _hubContext.Clients.Group(sessionToken).SendAsync("SessionCompleted", payload);

        public Task NotifySessionClosedAsync(string sessionToken, object payload)
            => _hubContext.Clients.Group(sessionToken).SendAsync("SessionClosed", payload);
    }
}
