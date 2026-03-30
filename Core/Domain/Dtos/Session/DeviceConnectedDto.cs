namespace Domain.Dtos.Session
{
    public class DeviceConnectedDto
    {
        public string SessionToken { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string ProductType { get; set; } = string.Empty;
    }

    public class DeviceConnectedResultDto
    {
        public long SessionId { get; set; }
        public string ResultMessage { get; set; } = string.Empty;
    }
}
