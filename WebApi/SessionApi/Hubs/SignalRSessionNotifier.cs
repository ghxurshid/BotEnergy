using Domain.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace SessionApi.Hubs
{
    /// <summary>
    /// ISessionNotifier ning SignalR implementatsiyasi.
    /// Service qatlami bu interfeys orqali klientlarga xabar yuboradi —
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

        public Task NotifySessionUpdatedAsync(string sessionToken, object payload)
            => _hubContext.Clients.Group(sessionToken).SendAsync("SessionUpdated", payload);

        public Task NotifySessionClosedAsync(string sessionToken, object payload)
            => Task.WhenAll(
                _hubContext.Clients.Group(sessionToken).SendAsync("SessionUpdated", payload),
                _hubContext.Clients.Group(sessionToken).SendAsync("SessionClosed", payload));

        public Task NotifyProcessStartedAsync(string sessionToken, object payload)
            => _hubContext.Clients.Group(sessionToken).SendAsync("ProcessStarted", payload);

        public Task NotifyProcessUpdatedAsync(string sessionToken, object payload)
            => _hubContext.Clients.Group(sessionToken).SendAsync("ProcessUpdated", payload);

        public Task NotifyProcessEndedAsync(string sessionToken, object payload)
            => _hubContext.Clients.Group(sessionToken).SendAsync("ProcessEnded", payload);

        public Task NotifyProcessStoppingAsync(string sessionToken, object payload)
            => _hubContext.Clients.Group(sessionToken).SendAsync("ProcessStopping", payload);

        public Task NotifyProcessPausingAsync(string sessionToken, object payload)
            => _hubContext.Clients.Group(sessionToken).SendAsync("ProcessPausing", payload);

        public Task NotifyUserAsync(long userId, string eventName, object payload)
            => _hubContext.Clients.Group(SessionHub.UserGroup(userId)).SendAsync(eventName, payload);

        public Task NotifySessionBalanceChangedAsync(string sessionToken, long userId, Domain.Dtos.PaymentSession.SessionBalanceChangedDto e)
            => Task.WhenAll(
                _hubContext.Clients.Group(sessionToken).SendAsync("SessionBalanceChanged", e),
                _hubContext.Clients.Group(SessionHub.UserGroup(userId)).SendAsync("SessionBalanceChanged", e));

        public Task NotifyDeviceStatusAsync(Domain.Dtos.Device.DeviceStatusChangedDto e)
            => Task.WhenAll(
                _hubContext.Clients.Group(SessionHub.DeviceGroup(e.DeviceId)).SendAsync("DeviceStatusChanged", e),
                _hubContext.Clients.Group(SessionHub.MerchantGroup(e.MerchantId)).SendAsync("DeviceStatusChanged", e));
    }
}
