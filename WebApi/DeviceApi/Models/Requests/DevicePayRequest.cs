namespace DeviceApi.Models.Requests
{
    public class DevicePayRequest
    {
        public string DeviceId { get; set; }
        public string UserAppId { get; set; }
        public string QrCode { get; set; }
        public decimal Amount { get; set; }
    }
}
