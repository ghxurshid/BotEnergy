namespace UserApi.Models.Responses
{
    public class CreateSessionResponse
    {
        public long SessionId { get; set; }
        public string SessionToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
