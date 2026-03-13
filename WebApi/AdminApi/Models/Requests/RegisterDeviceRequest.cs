namespace AdminApi.Models.Requests
{
    public class RegisterDeviceRequest
    {
        public string DeviceId { get; set; }
        public int DeviceType { get; set; }
        public string Location { get; set; }
    }
}
