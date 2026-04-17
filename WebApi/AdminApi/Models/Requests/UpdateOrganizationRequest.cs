namespace AdminApi.Models.Requests
{
    public class UpdateOrganizationRequest
    {
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
        public bool? IsActive { get; set; }
    }
}
