namespace AdminApi.Models.Requests
{
    public class UpdateStationRequest
    {
        public string? Name { get; set; }
        public string? Location { get; set; }
        public bool? IsActive { get; set; }
    }
}
