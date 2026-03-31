namespace UserApi.Models.Requests
{
    public class InternalDeviceProgressRequest
    {
        public string SerialNumber { get; set; } = string.Empty;
        public string SessionToken { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
    }
}
