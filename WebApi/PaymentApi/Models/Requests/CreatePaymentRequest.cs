namespace PaymentApi.Models.Requests
{
    public class CreatePaymentRequest
    {
        public string UserId { get; set; }
        public decimal Amount { get; set; }
    }
}
