namespace AdminApi.Models.Requests
{
    public class UpdateDeviceRequest
    {
        public string? Model { get; set; }
        public string? FirmwareVersion { get; set; }
        public bool? IsOnline { get; set; }
        public bool? IsActive { get; set; }
    }
}
