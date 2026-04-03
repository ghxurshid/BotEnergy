namespace DeviceApi.Models.Requests
{
    public class DeviceProcessRequest
    {
        public required string DeviceId { get; set; }
        public required string ProductId { get; set; }
        public required string UnitType { get; set; }
        public decimal Amount { get; set; }
        public required string UserAppId { get; set; }
        public required string BeginEnd { get; set; }
        public string? EndReason { get; set; }
    }
}
