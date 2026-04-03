namespace AdminApi.Models.Requests
{
    public class ChangeDeviceStatusRequest
    {
        public required string DeviceId { get; set; }
        public required string Status { get; set; }
    }
}
