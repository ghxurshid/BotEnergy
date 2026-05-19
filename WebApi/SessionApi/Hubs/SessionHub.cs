using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace SessionApi.Hubs
{
    /// <summary>
    /// Server → Client real-time push uchun ishlatiladi.
    /// Klient sessiya guruhiga qo'shilishi va undan chiqishi mumkin — boshqa hech qanday
    /// state-modifying operatsiya hub orqali qabul qilinmaydi (ownership-check va
    /// audit-trail uchun barcha buyruqlar REST endpoint orqali yuboriladi).
    ///
    /// Group sxemasi:
    ///   - sessionToken — sessiyaga ulangan barcha klientlar
    ///   - "user:{userId}" — JWT'dan olingan userId, ulanganda avtomatik join
    ///
    /// Server → Client eventlar:
    ///   DeviceConnected   { device_id, products, ... }
    ///   ProcessStarted    { process_id, product_id, requested_amount, ... }
    ///   ProcessUpdated    { process_id, total_given, current_cost, product_id, unit, price_per_unit }
    ///   ProcessEnded      { process_id, end_reason, total_given, total_cost, ended_at }
    ///   SessionUpdated    { session_id, status, ... }
    ///   SessionClosed     { reason, total_delivered, total_cost, closed_at }  // session-level — barcha jarayonlar yig'indisi
    /// </summary>
    [Authorize]
    public sealed class SessionHub : Hub
    {
        private readonly ILogger<SessionHub> _logger;

        public SessionHub(ILogger<SessionHub> logger)
        {
            _logger = logger;
        }

        public static string UserGroup(long userId) => $"user:{userId}";

        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();
            if (userId.HasValue)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId.Value));
                _logger.LogDebug("Client {ConnectionId} auto-joined user group {UserId}", Context.ConnectionId, userId);
            }

            await base.OnConnectedAsync();
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

        private long? GetUserId()
        {
            var raw = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return long.TryParse(raw, out var id) ? id : null;
        }
    }
}
