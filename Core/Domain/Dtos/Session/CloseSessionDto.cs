namespace Domain.Dtos.Session
{
    public class CloseSessionDto
    {
        public long SessionId { get; set; }
        public long UserId { get; set; }
    }

    public class CloseSessionResultDto
    {
        public string ResultMessage { get; set; } = string.Empty;
        public decimal TotalDelivered { get; set; }
    }
}
