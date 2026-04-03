namespace AdminApi.Models.Requests
{
    public class CreateOrganizationRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Inn { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
    }
}
