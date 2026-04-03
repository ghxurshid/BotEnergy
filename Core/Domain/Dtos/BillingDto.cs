namespace Domain.Dtos
{
    public class GetBalanceResultDto
    {
        public long UserId { get; set; }
        public decimal Balance { get; set; }
        public string Currency { get; set; } = "UZS";
    }

    public class TopUpBalanceDto
    {
        public long UserId { get; set; }
        public decimal Amount { get; set; }
    }

    public class TopUpBalanceResultDto
    {
        public decimal NewBalance { get; set; }
        public required string ResultMessage { get; set; }
    }
}
