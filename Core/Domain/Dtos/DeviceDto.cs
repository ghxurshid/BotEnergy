using Domain.Enums;

namespace Domain.Dtos
{
    public class RegisterDeviceDto
    {
        public required string SerialNumber { get; set; }
        public DeviceType DeviceType { get; set; }
        public long StationId { get; set; }
        public string? Model { get; set; }
        public string? FirmwareVersion { get; set; }
        public int FunctionCount { get; set; } = 1;
    }

    public class UpdateDeviceDto
    {
        public string? Model { get; set; }
        public string? FirmwareVersion { get; set; }
        public bool? IsActive { get; set; }
        public long? StationId { get; set; }
    }

    public class DeviceItemDto
    {
        public long Id { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public DeviceType DeviceType { get; set; }
        public string? Model { get; set; }
        public string? FirmwareVersion { get; set; }
        public int FunctionCount { get; set; }
        public long StationId { get; set; }
        public string StationName { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class DeviceResultDto
    {
        public long Id { get; set; }
        public required string ResultMessage { get; set; }
    }
}
