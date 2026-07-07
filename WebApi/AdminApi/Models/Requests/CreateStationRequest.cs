namespace AdminApi.Models.Requests
{
    public class CreateStationRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public long MerchantId { get; set; }
    }
}
