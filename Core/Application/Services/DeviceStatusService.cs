using Domain.Dtos.Device;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Services
{
    /// <summary>
    /// Qurilma ulanish holatining yagona o'zgartirish + real-time xabar berish nuqtasi.
    /// Edge-triggered — faqat haqiqiy o'tishda event chiqaradi (storm yo'q).
    /// </summary>
    public class DeviceStatusService : IDeviceStatusService
    {
        private readonly IDeviceRepository _deviceRepo;
        private readonly ISessionNotifier _notifier;
        // ISessionService kech resolve qilinadi — SessionService allaqachon IDeviceStatusService'ga
        // bog'liq bo'lgani uchun konstruktorda inject qilsak DI sikli hosil bo'ladi.
        private readonly IServiceProvider _services;

        public DeviceStatusService(IDeviceRepository deviceRepo, ISessionNotifier notifier, IServiceProvider services)
        {
            _deviceRepo = deviceRepo;
            _notifier = notifier;
            _services = services;
        }

        public async Task MarkSeenAsync(string serialNumber)
        {
            var becameOnline = await _deviceRepo.MarkSeenAsync(serialNumber);
            if (!becameOnline)
                return; // allaqachon online — event yo'q

            var info = await _deviceRepo.GetStatusInfoBySerialAsync(serialNumber);
            if (info is null)
                return;

            await _notifier.NotifyDeviceStatusAsync(Build(info, DeviceConnectivity.Online, sessionId: null));

            // Offline→online edge — Paused sessiyalarni avto-resume qilamiz.
            var sessionService = _services.GetRequiredService<ISessionService>();
            await sessionService.ResumePausedSessionsForDeviceAsync(info.DeviceId);
        }

        public async Task NotifyOfflineAsync(string serialNumber, bool lost, long? sessionId)
        {
            var info = await _deviceRepo.GetStatusInfoBySerialAsync(serialNumber);
            if (info is null)
                return;

            var status = lost ? DeviceConnectivity.Lost : DeviceConnectivity.Offline;
            await _notifier.NotifyDeviceStatusAsync(Build(info, status, sessionId));
        }

        private static DeviceStatusChangedDto Build(DeviceStatusInfo info, DeviceConnectivity status, long? sessionId)
            => new(
                DeviceId: info.DeviceId,
                Serial: info.Serial,
                StationId: info.StationId,
                MerchantId: info.MerchantId,
                Status: status.ToString(),
                IsOnline: status == DeviceConnectivity.Online,
                LastSeenAt: info.LastSeenAt,
                SessionId: sessionId,
                Timestamp: DateTime.Now);
    }
}
