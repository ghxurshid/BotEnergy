namespace Domain.Interfaces
{
    /// <summary>
    /// Sessiya va process hodisalarini real-time klientlarga yetkazish uchun abstraktsiya.
    /// Implementatsiya SignalR, WebSocket yoki boshqa transport bo'lishi mumkin.
    ///
    /// Group sxemasi:
    ///   - sessionToken — sessiyaga ulangan barcha klientlar uchun (planshet+telefon)
    ///   - "user:{userId}" — foydalanuvchining hamma device lari uchun (sessionToken bilmasdan ham keladi)
    /// </summary>
    public interface ISessionNotifier
    {
        Task NotifyDeviceConnectedAsync(string sessionToken, object payload);
        Task NotifySessionUpdatedAsync(string sessionToken, object payload);
        Task NotifySessionClosedAsync(string sessionToken, object payload);

        Task NotifyProcessStartedAsync(string sessionToken, object payload);
        Task NotifyProcessUpdatedAsync(string sessionToken, object payload);
        Task NotifyProcessEndedAsync(string sessionToken, object payload);

        /// <summary>
        /// Transient holatlar — buyruq qurilmaga yuborildi, lekin qurilma hali tasdiqlamadi.
        /// DB'ga yozilmaydi; klient tugmalarni disable qilib, yakuniy event (ProcessEnded/Paused)
        /// kelguncha "to'xtatilmoqda/pauza qilinmoqda" ko'rsatadi. Inersiya oynasi uchun.
        /// </summary>
        Task NotifyProcessStoppingAsync(string sessionToken, object payload);
        Task NotifyProcessPausingAsync(string sessionToken, object payload);

        /// <summary>
        /// Foydalanuvchi guruhiga (user:{userId}) ixtiyoriy event yuborish.
        /// Klient SignalR hub'ga ulanishi bilan avtomatik shu group'ga qo'shiladi.
        /// </summary>
        Task NotifyUserAsync(long userId, string eventName, object payload);

        /// <summary>
        /// Sessiya hold balansi o'zgardi (invoice held/consumed/captured/refunded).
        /// Session group + user group'ga yuboriladi; MQTT balance.update bilan BIR XIL payload.
        /// Event nomi: <c>SessionBalanceChanged</c>.
        /// </summary>
        Task NotifySessionBalanceChangedAsync(string sessionToken, long userId, Domain.Dtos.PaymentSession.SessionBalanceChangedDto e);

        /// <summary>
        /// Qurilma holati o'zgarganini (online/offline/lost) tracking qilayotgan guruhlarga yuboradi:
        /// <c>device:{DeviceId}</c> (app session ekrani, admin device sahifasi) va
        /// <c>merchant:{MerchantId}</c> (admin device ro'yxati). Event nomi: <c>DeviceStatusChanged</c>.
        /// </summary>
        Task NotifyDeviceStatusAsync(Domain.Dtos.Device.DeviceStatusChangedDto e);
    }
}
