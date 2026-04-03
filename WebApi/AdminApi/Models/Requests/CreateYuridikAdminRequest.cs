namespace AdminApi.Models.Requests
{
    public class CreateYuridikAdminRequest
    {
        public required string PhoneNumber { get; set; }
        public required string Inn { get; set; }
    }
}
