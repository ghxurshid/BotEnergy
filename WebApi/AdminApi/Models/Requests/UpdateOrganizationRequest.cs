namespace AdminApi.Models.Requests
{
    public class UpdateOrganizationRequest
    {
        public string? Name { get; set; }
        public string? Inn { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
    }
}
