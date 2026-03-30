namespace DeviceApi.Mqtt.Payloads
{
    public class DeviceFinishPayload
    {
        public string SessionToken { get; set; } = string.Empty;
        public decimal FinalQuantity { get; set; }
    }
}
