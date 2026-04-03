namespace UsageSessionApi.Models.Requests
{
    public class CreateSessionRequest
    {
        public long ProductId { get; set; }
        public decimal? RequestedQuantity { get; set; }
    }
}
