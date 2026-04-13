using CommonConfiguration.Messaging;
using Domain.Messaging;
using Domain.Messaging.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace UserApi.Hubs
{
    /// <summary>
    /// Real-time sessiya boshqaruvi.
    /// Android/iOS klient shu hub ga ulanib sessiyani kuzatadi va boshqaradi.
    ///
    /// Client → Server:
    ///   JoinSession(sessionToken)   — sessiya guruhiga qo'shilish
    ///   LeaveSession(sessionToken)  — guruhdan chiqish
    ///   PauseSession(serialNumber)  — qurilmani pauza qilish (RabbitMQ → DeviceApi → MQTT)
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
    public sealed class SessionHub : Hub
    {
        private readonly RabbitMqPublisher _publisher;
        private readonly ILogger<SessionHub> _logger;

        public SessionHub(RabbitMqPublisher publisher, ILogger<SessionHub> logger)
        {
            _publisher = publisher;
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

        public Task PauseSession(string deviceSerialNumber)
        {
            _publisher.Publish(QueueNames.CommandQueue, new DeviceCommand
            {
                CommandType = DeviceCommandTypes.Pause,
                SerialNumber = deviceSerialNumber
            });
            return Task.CompletedTask;
        }

        public Task ResumeSession(string deviceSerialNumber)
        {
            _publisher.Publish(QueueNames.CommandQueue, new DeviceCommand
            {
                CommandType = DeviceCommandTypes.Resume,
                SerialNumber = deviceSerialNumber
            });
            return Task.CompletedTask;
        }

        public Task StopSession(string deviceSerialNumber)
        {
            _publisher.Publish(QueueNames.CommandQueue, new DeviceCommand
            {
                CommandType = DeviceCommandTypes.Stop,
                SerialNumber = deviceSerialNumber
            });
            return Task.CompletedTask;
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogDebug("Client {ConnectionId} disconnected", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
