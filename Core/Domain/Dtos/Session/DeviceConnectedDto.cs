namespace Domain.Dtos.Session
{
    /// <summary>
    /// Qurilma sessiyaga ulanishi (MQTT event-dan keladi).
    /// Sessiyada hech qanday product hali tanlanmaydi — capability list qaytariladi.
    /// </summary>
    public class DeviceConnectedDto
    {
        public string SessionToken { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
    }

    public class DeviceConnectedResultDto
    {
        public long SessionId { get; set; }
        public long DeviceId { get; set; }
        public string DeviceSerialNumber { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public List<DeviceProductCapabilityDto> Products { get; set; } = new();
        public string ResultMessage { get; set; } = string.Empty;
    }

    public class DeviceProductCapabilityDto
    {
        public long ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}
