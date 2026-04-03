namespace UsageSessionApi.Models.Requests
{
    public class DeviceConnectRequest
    {
        public string SessionToken { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
    }
}
