namespace AdminApi.Models.Requests
{
    public class CreateStationRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Location { get; set; }
        public long MerchantId { get; set; }
    }
}
