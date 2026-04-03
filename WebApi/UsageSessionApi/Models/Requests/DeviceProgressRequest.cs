namespace UsageSessionApi.Models.Requests
{
    public class DeviceProgressRequest
    {
        public string SessionToken { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
    }
}
