namespace UserApi.Models.Requests
{
    public class ConnectDeviceRequest
    {
        public required string PhoneId { get; set; }
        public required string DeviceId { get; set; }
    }
}
