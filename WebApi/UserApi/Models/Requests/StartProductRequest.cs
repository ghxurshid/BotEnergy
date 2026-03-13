namespace UserApi.Models.Requests
{
    public class StartProductRequest
    {
        public string PhoneId { get; set; }
        public string ProductId { get; set; }
        public decimal MaxAmount { get; set; }
    }
}
