namespace BillingApi.Models.Requests
{
    public class AddBalanceRequest
    {
        public string UserId { get; set; }
        public decimal Amount { get; set; }
    }
}
