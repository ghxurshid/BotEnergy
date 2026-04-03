namespace UsageSessionApi.Models.Requests
{
    public class StartSessionRequest
    {
        public string DeviceSerialNumber { get; set; } = string.Empty;
        public long ProductId { get; set; }
        public decimal Amount { get; set; }
    }
}
