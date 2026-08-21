using Domain.Entities;

namespace Domain.Guards
{
    /// <summary>
    /// "Qurilmaga buyruq yuborish mumkinmi?" savoliga yagona javob.
    ///
    /// Nega alohida: MQTT publish brokerga muvaffaqiyatli ketadi, qurilma o'chgan bo'lsa ham —
    /// ya'ni publish natijasi "buyruq bajarildi"ni ANGLATMAYDI. Shuning uchun buyruqdan oldin
    /// qurilma haqiqatan eshitayotgani DB holati bo'yicha tekshiriladi.
    ///
    /// <see cref="DeviceEntity.IsOnline"/> yolg'iz yetarli emas: uni oflaynga o'tkazadigan fon
    /// servisi 30 soniyada bir ishlaydi, ya'ni bayroq 90–120 soniya "online" bo'lib turishi mumkin.
    /// Shu bo'shliqni yopish uchun <see cref="DeviceEntity.LastSeenAt"/> ham tekshiriladi.
    /// </summary>
    public static class DeviceAvailability
    {
        /// <summary>
        /// Shu vaqtdan uzoq jim turgan qurilma oflayn deb hisoblanadi.
        /// <c>SessionService.PauseOfflineDeviceSessionsAsync</c> ham shu chegaradan foydalanadi —
        /// ikki joyda ikki xil chegara bo'lsa, "online ko'rinadi lekin buyruq yetmaydi" holati chiqadi.
        /// </summary>
        public static readonly TimeSpan OfflineThreshold = TimeSpan.FromSeconds(90);

        /// <summary>Qurilma hozir buyruqni qabul qila oladimi (faol + online + yaqinda aloqada bo'lgan).</summary>
        public static bool IsReachable(this DeviceEntity device)
            => device.IsActive
               && device.IsOnline
               && device.LastSeenAt.HasValue
               && DateTime.Now - device.LastSeenAt.Value <= OfflineThreshold;

        /// <summary>Qurilma buyruq qabul qila olmasa — sababini qaytaradi, aks holda <c>null</c>.</summary>
        public static StopFactor? ReachabilityStopFactor(this DeviceEntity? device)
        {
            if (device is null)
                return StopFactors.Device.NotFound;

            if (!device.IsActive)
                return StopFactors.Device.Inactive;

            if (!device.IsReachable())
                return StopFactors.Device.Offline(device.SerialNumber, device.LastSeenAt);

            return null;
        }
    }
}
