namespace AdminApi.Models.Requests
{
    public class CreateOrganizationRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Inn { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public decimal? Balance { get; set; }
        public bool? IsActive { get; set; }
    }
}
