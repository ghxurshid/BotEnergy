namespace DeviceApi.Models.Requests
{
    public class DeviceProcessRequest
    {
        public string DeviceId { get; set; }
        public string ProductId { get; set; }
        public string UnitType { get; set; }
        public decimal Amount { get; set; }
        public string UserAppId { get; set; }
        public string BeginEnd { get; set; }
        public string EndReason { get; set; }
    }
}
