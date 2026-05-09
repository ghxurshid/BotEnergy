namespace Domain.Dtos.Session
{
    public class HeartbeatResultDto
    {
        public long SessionId { get; set; }
        public DateTime LastActivityAt { get; set; }
        public DateTime IdleAfter { get; set; }
    }
}
