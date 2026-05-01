namespace UserApi.Models.Responses
{
    public class StartProcessResponse
    {
        public long ProcessId { get; set; }
        public long ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal PricePerUnit { get; set; }
        public decimal LimitAmount { get; set; }
        public string DeviceSerialNumber { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
