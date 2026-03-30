namespace UserApi.Models.Requests
{
    public class InternalDeviceConnectRequest
    {
        public string SerialNumber { get; set; } = string.Empty;
        public string SessionToken { get; set; } = string.Empty;
        public string ProductType { get; set; } = string.Empty;
    }
}
