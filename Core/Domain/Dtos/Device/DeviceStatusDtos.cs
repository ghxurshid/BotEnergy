namespace Domain.Dtos.Device
{
    /// <summary>
    /// Qurilma holati haqida yengil proyeksiya (scope/event uchun) — DB'dan join bilan olinadi.
    /// </summary>
    public sealed record DeviceStatusInfo(
        long DeviceId,
        string Serial,
        long StationId,
        long MerchantId,
        bool IsOnline,
        System.DateTime? LastSeenAt);

    /// <summary>
    /// Real-time "DeviceStatusChanged" event payloadi. Edge-triggered (faqat holat haqiqatan o'zgarganda).
    /// SignalR guruhlari: <c>device:{DeviceId}</c>, <c>merchant:{MerchantId}</c>.
    /// </summary>
    public sealed record DeviceStatusChangedDto(
        long DeviceId,
        string Serial,
        long StationId,
        long MerchantId,
        string Status,        // Online | Offline | Lost
        bool IsOnline,
        System.DateTime? LastSeenAt,
        long? SessionId,
        System.DateTime Timestamp);
}
