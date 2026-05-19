namespace Domain.Messaging.Events
{
    /// <summary>
    /// SessionApi MqttBridge → RabbitMQ → DeviceEventConsumer orqali keladigan qurilma hodisalari.
    /// <see cref="TotalGiven"/> — telemetry yoki finished eventda qurilma jami bergan miqdor (cumulative).
    /// </summary>
    public class DeviceEvent
    {
        public string EventType { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string? SessionToken { get; set; }
        public long? ProcessId { get; set; }
        public long? Sequence { get; set; }
        public decimal? TotalGiven { get; set; }
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
