using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UsageSessionApi.Mqtt;

namespace UsageSessionApi.Hubs
{
    /// <summary>
    /// Real-time sessiya boshqaruvi.
    /// Android/iOS klient shu hub ga ulanib sessiyani kuzatadi va boshqaradi.
    ///
    /// Client → Server:
    ///   JoinSession(sessionToken)   — sessiya guruhiga qo'shilish
    ///   LeaveSession(sessionToken)  — guruhdan chiqish
    ///   PauseSession(serialNumber)  — qurilmani pauza qilish
    ///   ResumeSession(serialNumber) — qurilmani davom ettirish
    ///   StopSession(serialNumber)   — qurilmani to'xtatish
    ///
    /// Server → Client (events):
    ///   DeviceConnected   { serial_number, product_id, ... }
    ///   ProgressUpdate    { quantity, total_quantity, ... }
    ///   SessionCompleted  { total_delivered, end_reason, ... }
    ///   SessionClosed     { reason, total_delivered, ... }
    /// </summary>
    [Authorize]
    public sealed class UsageSessionHub : Hub
    {
        private readonly MqttSessionBridge _mqttBridge;
        private readonly ILogger<UsageSessionHub> _logger;

        public UsageSessionHub(MqttSessionBridge mqttBridge, ILogger<UsageSessionHub> logger)
        {
            _mqttBridge = mqttBridge;
            _logger = logger;
        }

        public async Task JoinSession(string sessionToken)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, sessionToken);
            _logger.LogDebug("Client {ConnectionId} joined session {Token}", Context.ConnectionId, sessionToken);
        }

        public async Task LeaveSession(string sessionToken)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionToken);
            _logger.LogDebug("Client {ConnectionId} left session {Token}", Context.ConnectionId, sessionToken);
        }

        public async Task PauseSession(string deviceSerialNumber)
        {
            await _mqttBridge.PublishPauseCommandAsync(deviceSerialNumber);
        }

        public async Task ResumeSession(string deviceSerialNumber)
        {
            await _mqttBridge.PublishResumeCommandAsync(deviceSerialNumber);
        }

        public async Task StopSession(string deviceSerialNumber)
        {
            await _mqttBridge.PublishStopCommandAsync(deviceSerialNumber);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogDebug("Client {ConnectionId} disconnected", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
