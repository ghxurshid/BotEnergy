namespace UserApi.Models.Responses
{
    public class SetQuantityResponse
    {
        public decimal LimitQuantity { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal PricePerUnit { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
