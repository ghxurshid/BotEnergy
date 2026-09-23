namespace Domain.Interfaces
{
    /// <summary>
    /// Kolonka ekranida ko'rsatiladigan **bir martalik** QR kodlar ombori (cache).
    ///
    /// Qurilma o'z ekranida QR chizishdan oldin serverdan kod so'raydi; kod qisqa
    /// muddatli (TTL) bo'lib, faqat bitta qurilmaga bog'langan. Mijoz uni
    /// skanerlab sessiya ochgach kod darhol iste'mol qilinadi — takroriy
    /// skanerlash (screenshot/replay) ishlamaydi.
    ///
    /// DB'ga yozilmaydi: TTL tugashi bilan o'zi yo'qoladi.
    /// </summary>
    public interface IDeviceQrStore
    {
        /// <summary>
        /// Kodni saqlaydi va uni qurilmaning joriy kodi sifatida belgilaydi.
        /// Qurilmada avvalgi kod bo'lsa — u bekor qilinadi (bir vaqtda bitta amaldagi kod).
        /// </summary>
        Task SetAsync(string code, DeviceQrEntry entry, TimeSpan ttl);

        /// <summary>Kod bo'yicha yozuvni o'qiydi (muddati o'tgan bo'lsa null).</summary>
        Task<DeviceQrEntry?> GetAsync(string code);

        /// <summary>
        /// Kodni atomik iste'mol qiladi: birinchi chaqiruv yozuvni qaytaradi va o'chiradi,
        /// keyingilari null oladi. Ikki mijoz bir vaqtda skanerlasa faqat bittasi o'tadi.
        /// </summary>
        Task<DeviceQrEntry?> ConsumeAsync(string code);

        /// <summary>Qurilmaning amaldagi kodini bekor qiladi (sessiya yopilganda va h.k.).</summary>
        Task InvalidateForDeviceAsync(long deviceId);
    }

    /// <summary>Bir martalik QR kod yozuvi.</summary>
    public sealed record DeviceQrEntry(long DeviceId, string SerialNumber, DateTime ExpiresAt);
}
