namespace AdminApi.Models.Requests
{
    public class CreateRoleRequest
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
        public long OrganizationId { get; set; } = 0;
    }
}
