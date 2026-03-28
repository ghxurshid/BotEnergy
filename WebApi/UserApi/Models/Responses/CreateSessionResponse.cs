namespace UserApi.Models.Responses
{
    public class CreateSessionResponse
    {
        public long SessionId { get; set; }
        public string SessionToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public string ResultMessage { get; set; } = string.Empty;
    }
}
