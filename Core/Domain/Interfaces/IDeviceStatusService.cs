namespace Domain.Interfaces
{
    /// <summary>
    /// Qurilma ulanish holatining (online/offline/lost) yagona o'zgartirish + xabar berish nuqtasi.
    /// Edge-triggered: faqat holat haqiqatan o'zgarganda event chiqaradi.
    /// SessionApi'da implement qilinadi (ISessionNotifier'ga bog'liq).
    /// </summary>
    public interface IDeviceStatusService
    {
        /// <summary>
        /// Inbound MQTT xabarida chaqiriladi. LastSeenAt yangilanadi; agar qurilma oldin
        /// offline bo'lib endi online bo'lsa (edge) — <c>DeviceStatusChanged{Online}</c> chiqariladi.
        /// </summary>
        Task MarkSeenAsync(string serialNumber);

        /// <summary>
        /// Offline aniqlangach (idle cleaner) chaqiriladi — <c>DeviceStatusChanged{Offline|Lost}</c>.
        /// IsOnline allaqachon false ga o'rnatilgan bo'lishi kutiladi.
        /// </summary>
        Task NotifyOfflineAsync(string serialNumber, bool lost, long? sessionId);
    }
}
