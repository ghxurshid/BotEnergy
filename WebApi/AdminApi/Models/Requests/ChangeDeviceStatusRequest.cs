namespace AdminApi.Models.Requests
{
    public class ChangeDeviceStatusRequest
    {
        public string DeviceId { get; set; }
        public string Status { get; set; }
    }
}
