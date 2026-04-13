namespace CommonConfiguration.Redis
{
    /// <summary>
    /// Qurilmani exclusive lock qilish — bir vaqtda faqat bitta foydalanuvchi bitta qurilmani boshqaradi.
    /// </summary>
    public interface IDeviceLockService
    {
        /// <summary>Qurilmani band qilish. Agar band bo'lsa false qaytaradi.</summary>
        Task<bool> TryLockDeviceAsync(string serialNumber, long userId, TimeSpan? expiry = null);

        /// <summary>Qurilmani bo'shatish. Faqat band qilgan user bo'shatishi mumkin.</summary>
        Task<bool> UnlockDeviceAsync(string serialNumber, long userId);

        /// <summary>Qurilma qaysi user tomonidan band qilinganini tekshirish.</summary>
        Task<long?> GetLockOwnerAsync(string serialNumber);
    }
}
