namespace DeviceApi.Services
{
    /// <summary>
    /// Qurilma reader QR kodni o'qib MQTT orqali userId+sessionToken yuborganda chaqiriladi.
    /// Token UserApi cache'idan gRPC orqali olinadi, solishtiriladi, mos kelsa DB'da sessiya yaratiladi
    /// va RabbitMQ orqali UserApi'ga "connected" event yuboriladi.
    /// </summary>
    public interface IDeviceSessionService
    {
        Task<DeviceConnectResult> TryConnectAsync(
            string serialNumber,
            long userId,
            string sessionToken,
            CancellationToken ct);
    }

    /// <summary>
    /// Connect oqimining yakuniy natijasi. <c>Code</c> har doim to'ldiriladi
    /// (<see cref="ConnectResultCodes"/> dan), <c>Message</c> ham (lokalizatsiya kelajakda mumkin),
    /// <c>SessionId</c> faqat Success holatida.
    /// </summary>
    public sealed record DeviceConnectResult(
        bool Success,
        string Code,
        string Message,
        long? SessionId);
}
