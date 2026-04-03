namespace UsageSessionApi.Models.Responses
{
    public class CreateSessionResponse
    {
        public long SessionId { get; set; }
        public string SessionToken { get; set; } = string.Empty;
        public decimal LimitQuantity { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal PricePerUnit { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
