namespace AdminApi.Models.Requests
{
    public class UpdateStationRequest
    {
        public string? Name { get; set; }
        public string? Address { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public bool? IsActive { get; set; }
    }
}
