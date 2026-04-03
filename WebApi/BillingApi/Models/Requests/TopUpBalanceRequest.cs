namespace BillingApi.Models.Requests
{
    public class TopUpBalanceRequest
    {
        public long UserId { get; set; }
        public decimal Amount { get; set; }
    }
}
