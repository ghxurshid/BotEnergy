namespace DeviceApi.Models.Responses
{
    public class DeviceProcessResponse
    {
        public decimal LimitAmount { get; set; }
        public string ProductId { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
    }
}
