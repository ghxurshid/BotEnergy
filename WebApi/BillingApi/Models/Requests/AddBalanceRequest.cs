namespace BillingApi.Models.Requests
{
    public class AddBalanceRequest
    {
        public required string UserId { get; set; }
        public decimal Amount { get; set; }
    }
}
