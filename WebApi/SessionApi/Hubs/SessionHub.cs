using CommonConfiguration.Extensions;
using Domain.Auth;
using Domain.Dtos.Device;
using Domain.Guards;
using Domain.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SessionApi.Hubs
{
    /// <summary>
    /// Server → Client real-time push. Klient guruhlarga obuna bo'ladi (state-modifying yo'q).
    ///
    /// Group sxemasi:
    ///   - sessionToken            — sessiyaga ulangan klientlar (planshet+telefon)
    ///   - "user:{userId}"         — JWT'dan userId, ulanganda avtomatik join (faqat Customer)
    ///   - "device:{deviceId}"     — BITTA qurilmaning online/offline holati. Kim kuzatsa
    ///                               (mijoz ilovasi, inkassator ilovasi, admin paneli) —
    ///                               hammasi shu bitta guruhda; server bitta xabar yuboradi.
    ///   - "station:{stationId}"   — stansiyaning barcha qurilmalari (stansiya ekrani)
    ///   - "merchant:{merchantId}" — merchant qurilmalari ro'yxati (admin)
    ///
    /// Eventlar: DeviceConnected, ProcessStarted/Updated/Ended, SessionUpdated/Closed,
    ///           DeviceStatusChanged { deviceId, serial, status, isOnline, ... },
    ///           DeviceStatusSnapshot [ ...DeviceStatusChanged ]  (obuna paytida joriy holat)
    ///
    /// Watcher oqimi (ro'yxatdagi yashil/qizil indikator):
    ///   1) klient ekrandagi qurilma id'larini <see cref="SubscribeDevices"/> ga BITTA chaqiruvda beradi;
    ///   2) darhol <c>DeviceStatusSnapshot</c> keladi — ro'yxat boshlang'ich holatda to'g'ri chiziladi;
    ///   3) keyin holat o'zgargan sari <c>DeviceStatusChanged</c> keladi (edge-triggered, storm yo'q);
    ///   4) uzilib qayta ulanganda klient 1-qadamni takrorlaydi — obunalar ham, snapshot ham tiklanadi.
    ///
    /// Autentifikatsiya: SessionApi REST sirti Customer-audience'li, lekin hub qo'shimcha
    /// <c>PlatformBearer</c> sxemasini ham qabul qiladi — admin va inkassator ilovalari
    /// (Platform tokeni) qurilma statusini kuzata olishi uchun.
    /// </summary>
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme + "," + JwtSchemes.Platform)]
    public sealed class SessionHub : Hub
    {
        /// <summary>
        /// Bitta chaqiruvda obuna bo'lish mumkin bo'lgan qurilma soni. Ekranda ko'rinadigan
        /// ro'yxat uchun yetarli; cheksiz ro'yxat bilan hub'ni bo'g'ishning oldini oladi.
        /// </summary>
        public const int MaxDevicesPerCall = 200;

        private readonly ILogger<SessionHub> _logger;
        private readonly IDeviceRepository _deviceRepo;
        private readonly IStationRepository _stationRepo;

        public SessionHub(
            ILogger<SessionHub> logger,
            IDeviceRepository deviceRepo,
            IStationRepository stationRepo)
        {
            _logger = logger;
            _deviceRepo = deviceRepo;
            _stationRepo = stationRepo;
        }

        public static string UserGroup(long userId) => $"user:{userId}";
        public static string DeviceGroup(long deviceId) => $"device:{deviceId}";
        public static string StationGroup(long stationId) => $"station:{stationId}";
        public static string MerchantGroup(long merchantId) => $"merchant:{merchantId}";

        public override async Task OnConnectedAsync()
        {
            var scope = Context.User.GetScope();

            // user:{id} — sessiya/to'lov bildirishnomalari uchun; ular faqat Customer'ga tegishli.
            // Platform foydalanuvchisini bu guruhga qo'shsak, id'lar mos kelib qolganda
            // (platform user #7 va customer user #7 alohida jadvallarda) begona xabar ketardi.
            if (scope.IsCustomer && scope.UserId > 0)
                await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(scope.UserId));

            await base.OnConnectedAsync();
        }

        // ── Sessiya guruhi ──────────────────────────────────────────────
        /// <summary>
        /// Sessiya guruhiga qo'shilish. Faqat Customer: sessiya tokeni mijoz ilovasida bo'ladi,
        /// platforma tokeni bu hub'ga qurilma statusini kuzatish uchun kiritilgan — sessiya
        /// oqimini tinglash uchun emas.
        /// </summary>
        public Task JoinSession(string sessionToken)
        {
            if (!Context.User.GetScope().IsCustomer)
                throw new HubException("Sessiya guruhiga faqat mijoz ilovasi qo'shila oladi.");

            return Groups.AddToGroupAsync(Context.ConnectionId, sessionToken);
        }

        public Task LeaveSession(string sessionToken)
            => Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionToken);

        // ── Qurilma statusini kuzatish ──────────────────────────────────
        /// <summary>
        /// Ekrandagi qurilmalar ro'yxatiga bitta chaqiruvda obuna bo'lish + joriy holat snapshot'i.
        /// Ruxsat doirasidan tashqaridagi id'lar jimgina tashlab ketiladi (xato emas —
        /// klient ro'yxati aralash bo'lishi mumkin).
        /// </summary>
        public async Task SubscribeDevices(long[] deviceIds)
        {
            var ids = Normalize(deviceIds);
            if (ids.Count == 0)
                return;

            var scope = Context.User.GetScope();
            var infos = await _deviceRepo.GetStatusInfoByIdsAsync(ids);
            var allowed = infos.Where(i => CanWatchDevice(scope, i.MerchantId)).ToList();

            foreach (var info in allowed)
                await Groups.AddToGroupAsync(Context.ConnectionId, DeviceGroup(info.DeviceId));

            await SendSnapshotAsync(allowed);

            if (allowed.Count != ids.Count)
                _logger.LogDebug(
                    "SubscribeDevices: {Allowed}/{Requested} qurilma obuna qilindi (userId={UserId} subType={SubType}).",
                    allowed.Count, ids.Count, scope.UserId, scope.SubType);
        }

        public async Task UnsubscribeDevices(long[] deviceIds)
        {
            foreach (var id in Normalize(deviceIds))
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, DeviceGroup(id));
        }

        /// <summary>Bitta qurilma (sessiya ekrani) — <see cref="SubscribeDevices"/> ning qisqartmasi.</summary>
        public Task SubscribeDevice(long deviceId) => SubscribeDevices(new[] { deviceId });

        public Task UnsubscribeDevice(long deviceId)
            => Groups.RemoveFromGroupAsync(Context.ConnectionId, DeviceGroup(deviceId));

        /// <summary>
        /// Stansiyaning barcha qurilmalariga obuna (stansiya ekrani) + snapshot.
        /// Obuna qurilma id'siga emas, stansiyaga bog'langani uchun keyin qo'shilgan
        /// qurilma ham xabar beradi.
        /// </summary>
        public async Task SubscribeStation(long stationId)
        {
            var scope = Context.User.GetScope();

            // Platform foydalanuvchisi uchun stansiya merchanti tekshiriladi. Merchant qurilmadan
            // emas, stansiyaning o'zidan olinadi — aks holda qurilmasiz stansiyani baholab bo'lmasdi.
            if (scope.IsPlatform)
            {
                var station = await _stationRepo.GetByIdAsync(stationId);
                if (station is null || !scope.CanAccessMerchant(station.MerchantId))
                    throw new HubException("Bu stansiya qurilmalarini kuzatishga ruxsatingiz yo'q.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, StationGroup(stationId));
            await SendSnapshotAsync(await _deviceRepo.GetStatusInfoByStationAsync(stationId));
        }

        public Task UnsubscribeStation(long stationId)
            => Groups.RemoveFromGroupAsync(Context.ConnectionId, StationGroup(stationId));

        /// <summary>Merchant qurilmalari ro'yxatiga obuna (scope tekshiriladi) + snapshot.</summary>
        public async Task SubscribeMerchant(long merchantId)
        {
            var scope = Context.User.GetScope();
            if (!scope.IsPlatform || !scope.CanAccessMerchant(merchantId))
                throw new HubException("Bu merchant qurilmalarini kuzatishga ruxsatingiz yo'q.");

            await Groups.AddToGroupAsync(Context.ConnectionId, MerchantGroup(merchantId));
            await SendSnapshotAsync(await _deviceRepo.GetStatusInfoByMerchantAsync(merchantId));
        }

        public Task UnsubscribeMerchant(long merchantId)
            => Groups.RemoveFromGroupAsync(Context.ConnectionId, MerchantGroup(merchantId));

        // ── Helpers ─────────────────────────────────────────────────────
        private Task SendSnapshotAsync(IEnumerable<DeviceStatusInfo> infos)
            => Clients.Caller.SendAsync("DeviceStatusSnapshot", infos.Select(ToDto).ToList());

        /// <summary>
        /// Snapshot DTO'si live event bilan bir xil shaklda — klientda bitta handler yetadi.
        /// IsOnline DB bayrog'idan emas, LastSeenAt bilan birga hisoblanadi: qurilmani offline
        /// deb belgilaydigan fon servisi 30 soniyada bir ishlagani uchun bayroq eskirgan bo'lishi mumkin.
        /// </summary>
        private static DeviceStatusChangedDto ToDto(DeviceStatusInfo i)
        {
            var online = i.IsReachable();
            return new DeviceStatusChangedDto(
                i.DeviceId, i.Serial, i.StationId, i.MerchantId,
                online ? "Online" : "Offline", online, i.LastSeenAt, null, DateTime.Now);
        }

        /// <summary>
        /// Qurilma holatini kim kuzata oladi:
        ///   - Platform/Manage   → hammasi;
        ///   - Platform/Merchant → faqat o'z merchanti qurilmalari;
        ///   - Customer          → ha (kolonka ishlayaptimi — mijoz uchun operatsion ma'lumot,
        ///                          u baribir shu qurilmada sessiya ochadi).
        /// Merchant bo'yicha ommaviy obuna esa admin ko'rinishi — u yerda Customer rad etiladi.
        /// </summary>
        private static bool CanWatchDevice(AccessScope scope, long merchantId)
            => scope.IsCustomer || scope.CanAccessMerchant(merchantId);

        private static List<long> Normalize(long[]? deviceIds)
            => deviceIds is null
                ? new List<long>()
                : deviceIds.Where(id => id > 0).Distinct().Take(MaxDevicesPerCall).ToList();
    }
}
