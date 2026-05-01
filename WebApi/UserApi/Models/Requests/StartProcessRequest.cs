namespace UserApi.Models.Requests
{
    public class StartProcessRequest
    {
        public long SessionId { get; set; }
        public long ProductId { get; set; }
        public decimal? RequestedAmount { get; set; }
    }
}
