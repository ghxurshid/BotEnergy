namespace Domain.Messaging.Events
{
    /// <summary>
    /// DeviceApi → RabbitMQ → UserApi orqali keladigan qurilma hodisalari.
    /// MQTT dan kelgan xabarlar shu formatda RabbitMQ ga uzatiladi.
    /// </summary>
    public class DeviceEvent
    {
        public string EventType { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string? SessionToken { get; set; }
        public long? ProcessId { get; set; }
        public long? Sequence { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? FinalQuantity { get; set; }
        public string? EndReason { get; set; }
        public string? StatusPayload { get; set; }
    }

    public static class DeviceEventTypes
    {
        public const string Connected = "connected";
        public const string Telemetry = "telemetry";
        public const string Finished = "finished";
        public const string Heartbeat = "heartbeat";
        public const string Status = "status";
    }
}
