namespace DeviceApi.Mqtt.Payloads
{
    public class DeviceProgressPayload
    {
        public string SessionToken { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal TotalQuantity { get; set; }
    }
}
