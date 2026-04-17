using Domain.Enums;

namespace AdminApi.Models.Requests
{
    public class RegisterDeviceRequest
    {
        public string SerialNumber { get; set; } = string.Empty;
        public DeviceType DeviceType { get; set; }
        public long StationId { get; set; }
        public string? Model { get; set; }
        public string? FirmwareVersion { get; set; }
        public bool? IsOnline { get; set; }
        public bool? IsActive { get; set; }
    }
}
