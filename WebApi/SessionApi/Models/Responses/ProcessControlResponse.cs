namespace UserApi.Models.Responses
{
    public class ProcessControlResponse
    {
        public long ProcessId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
