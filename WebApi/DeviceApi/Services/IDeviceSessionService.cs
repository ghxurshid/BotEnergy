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
    /// <c>Success=true</c> bo'lsa <c>SessionId</c> to'ldiriladi, <c>Reason</c> null.
    /// Aks holda <c>Reason</c> qurilmaga MQTT ack ichida qaytariladi (debug uchun).
    /// </summary>
    public sealed record DeviceConnectResult(bool Success, long? SessionId, string? Reason);
}
