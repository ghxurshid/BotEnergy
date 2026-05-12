namespace Domain.Interfaces
{
    /// <summary>
    /// UserApi'da QR yaratilgandan keyin, qurilma uni o'qiy olmaguncha cache'da turuvchi
    /// pending sessiya tokeni. TTL tugaganda yoki sessiya DeviceApi tomonidan DB'da yaratilgach
    /// avtomatik bekor bo'ladi. DB'ga yozilmaydi.
    /// </summary>
    public interface IPendingSessionStore
    {
        Task SetAsync(long userId, string sessionToken, TimeSpan ttl);
        Task<PendingSessionEntry?> GetAsync(long userId);
        Task DeleteAsync(long userId);
    }

    public sealed record PendingSessionEntry(string SessionToken, DateTime ExpiresAt);
}
