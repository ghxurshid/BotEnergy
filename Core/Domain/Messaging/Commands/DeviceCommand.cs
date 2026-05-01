namespace Domain.Messaging.Commands
{
    /// <summary>
    /// UserApi → RabbitMQ → DeviceApi → MQTT orqali qurilmaga yuboriladigan buyruqlar.
    /// ProcessId — qaysi jarayonga tegishli ekanligini bildiradi (idempotency va correlation uchun).
    /// </summary>
    public class DeviceCommand
    {
        public string CommandType { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public long ProcessId { get; set; }
        public long? ProductId { get; set; }
        public decimal? Amount { get; set; }
    }

    public static class DeviceCommandTypes
    {
        public const string Start = "start";
        public const string Pause = "pause";
        public const string Resume = "resume";
        public const string Stop = "stop";
    }
}
