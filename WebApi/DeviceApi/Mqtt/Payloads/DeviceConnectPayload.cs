namespace DeviceApi.Mqtt.Payloads
{
    public class DeviceConnectPayload
    {
        public string SessionToken { get; set; } = string.Empty;
        public string ProductType { get; set; } = string.Empty;
    }
}
