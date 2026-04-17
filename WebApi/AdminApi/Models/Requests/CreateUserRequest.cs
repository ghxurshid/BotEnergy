namespace AdminApi.Models.Requests
{
    public class CreateUserRequest
    {
        public string PhoneId { get; set; } = string.Empty;
        public string Mail { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public long RoleId { get; set; }
        public long? OrganizationId { get; set; }
        public long? StationId { get; set; }
    }
}
