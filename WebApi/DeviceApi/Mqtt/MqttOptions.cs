namespace DeviceApi.Mqtt
{
    public class MqttOptions
    {
        public string BrokerHost { get; set; } = "localhost";
        public int BrokerPort { get; set; } = 1883;
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string ClientId { get; set; } = "botenergy-device-service";

        // ── TLS (transport-level) ──────────────────────────────────────
        public bool UseTls { get; set; } = false;
        public bool AllowUntrustedCertificates { get; set; } = false;
        public string? ClientCertificatePath { get; set; }
        public string? ClientCertificatePassword { get; set; }

        // ── Application-level encryption ───────────────────────────────
        public bool EnableEncryption { get; set; } = false;
        public int MaxClockSkewSeconds { get; set; } = 60;
    }
}
