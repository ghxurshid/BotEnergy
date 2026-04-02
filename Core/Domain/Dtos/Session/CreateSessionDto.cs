namespace Domain.Dtos.Session
{
    public class CreateSessionDto
    {
        public long UserId { get; set; }
        public long ProductId { get; set; }
        public decimal? RequestedQuantity { get; set; }
    }

    public class CreateSessionResultDto
    {
        public long SessionId { get; set; }
        public string SessionToken { get; set; } = string.Empty;
        public decimal LimitQuantity { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal PricePerUnit { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string ResultMessage { get; set; } = string.Empty;
    }
}
