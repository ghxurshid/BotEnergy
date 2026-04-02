namespace UsageSessionApi.Mqtt
{
    public class MqttSessionOptions
    {
        public string BrokerHost { get; set; } = "localhost";
        public int BrokerPort { get; set; } = 1883;
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string ClientId { get; set; } = "botenergy-session-service";
    }
}
