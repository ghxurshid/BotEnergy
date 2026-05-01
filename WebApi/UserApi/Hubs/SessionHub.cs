using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace UserApi.Hubs
{
    /// <summary>
    /// Server → Client real-time push uchun ishlatiladi.
    /// Klient sessiya guruhiga qo'shilishi va undan chiqishi mumkin — boshqa hech qanday
    /// state-modifying operatsiya hub orqali qabul qilinmaydi (ownership-check va
    /// audit-trail uchun barcha buyruqlar REST endpoint orqali yuboriladi).
    ///
    /// Server → Client eventlar:
    ///   DeviceConnected   { device_id, products, ... }
    ///   ProcessStarted    { process_id, product_id, requested_amount, ... }
    ///   ProcessUpdated    { process_id, total_quantity, current_cost, ... }
    ///   ProcessEnded      { process_id, end_reason, total_cost, ... }
    ///   SessionUpdated    { ... }
    ///   SessionClosed     { reason, total_delivered, ... }
    /// </summary>
    [Authorize]
    public sealed class SessionHub : Hub
    {
        private readonly ILogger<SessionHub> _logger;

        public SessionHub(ILogger<SessionHub> logger)
        {
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

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogDebug("Client {ConnectionId} disconnected", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
