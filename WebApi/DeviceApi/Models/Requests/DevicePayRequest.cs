namespace DeviceApi.Models.Requests
{
    public class DevicePayRequest
    {
        public required string DeviceId { get; set; }
        public required string UserAppId { get; set; }
        public required string QrCode { get; set; }
        public decimal Amount { get; set; }
    }
}
