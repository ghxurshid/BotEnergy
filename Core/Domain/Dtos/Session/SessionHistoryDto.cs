namespace Domain.Dtos.Session
{
    public class SessionHistoryItemDto
    {
        public long SessionId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? CloseReason { get; set; }
        public string? DeviceSerialNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
    }
}
