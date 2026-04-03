namespace DeviceApi.Models.Requests
{
    public class DeviceInfoRequest
    {
        public required string DeviceId { get; set; }
        public int DeviceType { get; set; }
        public int FunctionsCount { get; set; }
        public List<string> FunctionTypes { get; set; } = [];
        public required string RemoteId { get; set; }
        public List<string> Statuses { get; set; } = [];
        public List<string> Errors { get; set; } = [];
    }
}
