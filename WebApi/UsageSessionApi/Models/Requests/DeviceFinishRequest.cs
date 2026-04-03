namespace UsageSessionApi.Models.Requests
{
    public class DeviceFinishRequest
    {
        public string SessionToken { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public decimal FinalQuantity { get; set; }
        public string? EndReason { get; set; }
    }
}
