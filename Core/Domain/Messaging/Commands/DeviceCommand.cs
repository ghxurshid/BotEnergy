namespace Domain.Messaging.Commands
{
    /// <summary>
    /// UserApi → RabbitMQ → DeviceApi → MQTT orqali qurilmaga yuboriladigan buyruqlar.
    /// </summary>
    public class DeviceCommand
    {
        public string CommandType { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
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
