using Domain.Dtos.Device;
using Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace SessionApi.Hubs
{
    /// <summary>
    /// Server → Client real-time push. Klient guruhlarga obuna bo'ladi (state-modifying yo'q).
    ///
    /// Group sxemasi:
    ///   - sessionToken          — sessiyaga ulangan klientlar (planshet+telefon)
    ///   - "user:{userId}"       — JWT'dan userId, ulanganda avtomatik join
    ///   - "device:{deviceId}"   — bitta qurilma statusini kuzatish (app session, admin device sahifasi)
    ///   - "merchant:{merchantId}" — merchant qurilmalari ro'yxatini kuzatish (admin)
    ///
    /// Eventlar: DeviceConnected, ProcessStarted/Updated/Ended, SessionUpdated/Closed,
    ///           DeviceStatusChanged { deviceId, serial, status, isOnline, ... },
    ///           DeviceStatusSnapshot [ ...DeviceStatusChanged ]  (obuna paytida joriy holat)
    /// </summary>
    [Authorize]
    public sealed class SessionHub : Hub
    {
        private readonly ILogger<SessionHub> _logger;
        private readonly IDeviceRepository _deviceRepo;

        public SessionHub(ILogger<SessionHub> logger, IDeviceRepository deviceRepo)
        {
            _logger = logger;
            _deviceRepo = deviceRepo;
        }

        public static string UserGroup(long userId) => $"user:{userId}";
        public static string DeviceGroup(long deviceId) => $"device:{deviceId}";
        public static string MerchantGroup(long merchantId) => $"merchant:{merchantId}";

        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();
            if (userId.HasValue)
                await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId.Value));

            await base.OnConnectedAsync();
        }

        // ── Sessiya guruhi ──────────────────────────────────────────────
        public Task JoinSession(string sessionToken)
            => Groups.AddToGroupAsync(Context.ConnectionId, sessionToken);

        public Task LeaveSession(string sessionToken)
            => Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionToken);

        // ── Qurilma statusini kuzatish ──────────────────────────────────
        /// <summary>Bitta qurilma statusiga obuna + joriy holatni darhol qaytaradi.</summary>
        public async Task SubscribeDevice(long deviceId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, DeviceGroup(deviceId));
            var info = await _deviceRepo.GetStatusInfoByIdAsync(deviceId);
            if (info is not null)
                await Clients.Caller.SendAsync("DeviceStatusChanged", ToDto(info));
        }

        public Task UnsubscribeDevice(long deviceId)
            => Groups.RemoveFromGroupAsync(Context.ConnectionId, DeviceGroup(deviceId));

        /// <summary>Merchant qurilmalari ro'yxatiga obuna (scope tekshiriladi) + snapshot.</summary>
        public async Task SubscribeMerchant(long merchantId)
        {
            if (!CanAccessMerchant(merchantId))
                throw new HubException("Bu merchant qurilmalarini kuzatishga ruxsatingiz yo'q.");

            await Groups.AddToGroupAsync(Context.ConnectionId, MerchantGroup(merchantId));

            var list = await _deviceRepo.GetStatusInfoByMerchantAsync(merchantId);
            await Clients.Caller.SendAsync("DeviceStatusSnapshot", list.Select(ToDto));
        }

        public Task UnsubscribeMerchant(long merchantId)
            => Groups.RemoveFromGroupAsync(Context.ConnectionId, MerchantGroup(merchantId));

        // ── Helpers ─────────────────────────────────────────────────────
        private static DeviceStatusChangedDto ToDto(DeviceStatusInfo i)
            => new(i.DeviceId, i.Serial, i.StationId, i.MerchantId,
                   i.IsOnline ? "Online" : "Offline", i.IsOnline, i.LastSeenAt, null, DateTime.Now);

        /// <summary>Manage → har doim; Platform/Merchant → faqat o'z merchanti; aks holda yo'q.</summary>
        private bool CanAccessMerchant(long merchantId)
        {
            var subType = Context.User?.FindFirstValue("UserSubType");
            if (string.Equals(subType, "Manage", StringComparison.OrdinalIgnoreCase))
                return true;

            var raw = Context.User?.FindFirstValue("MerchantId");
            return long.TryParse(raw, out var own) && own == merchantId;
        }

        private long? GetUserId()
        {
            var raw = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return long.TryParse(raw, out var id) ? id : null;
        }
    }
}
