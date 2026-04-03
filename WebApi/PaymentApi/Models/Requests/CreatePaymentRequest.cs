namespace PaymentApi.Models.Requests
{
    public class CreatePaymentRequest
    {
        public required string UserId { get; set; }
        public decimal Amount { get; set; }
    }
}
