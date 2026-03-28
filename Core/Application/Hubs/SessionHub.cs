using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Application.Hubs
{
    // SignalR events (Server -> Client):
    //   DeviceConnected    { device_id, product_type, connected_at }
    //   ProgressUpdate     { quantity, total_quantity, product_type }
    //   SessionCompleted   { total_delivered, product_type, ended_at }
    //   SessionClosed      { reason, total_delivered, ended_at }
    //   CommandReceived    command: "start"/"stop", data: {...}
    [Authorize]
    public class SessionHub : Hub
    {
        public async Task JoinSession(string sessionToken)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, sessionToken);
        }

        public async Task LeaveSession(string sessionToken)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionToken);
        }

        public async Task SendStartCommand(string sessionToken, decimal requestedQuantity)
        {
            await Clients.Group(sessionToken).SendAsync("CommandReceived", "start", new
            {
                requested_quantity = requestedQuantity
            });
        }

        public async Task SendStopCommand(string sessionToken)
        {
            await Clients.Group(sessionToken).SendAsync("CommandReceived", "stop", new { });
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}
