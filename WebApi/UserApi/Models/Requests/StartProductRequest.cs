namespace UserApi.Models.Requests
{
    public class StartProductRequest
    {
        public required string PhoneId { get; set; }
        public required string ProductId { get; set; }
        public decimal MaxAmount { get; set; }
    }
}
