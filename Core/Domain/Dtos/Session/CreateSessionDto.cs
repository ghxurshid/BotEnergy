namespace Domain.Dtos.Session
{
    public class CreateSessionDto
    {
        public long UserId { get; set; }
    }

    public class CreateSessionResultDto
    {
        public long SessionId { get; set; }
        public string SessionToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public string ResultMessage { get; set; } = string.Empty;
    }
}
