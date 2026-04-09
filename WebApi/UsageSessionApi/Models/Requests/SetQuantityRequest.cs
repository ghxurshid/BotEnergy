namespace UsageSessionApi.Models.Requests
{
    public class SetQuantityRequest
    {
        public long SessionId { get; set; }
        public decimal? RequestedQuantity { get; set; }
    }
}
