namespace AdminApi.Models.Requests
{
    public class CreateRoleRequest
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
        public long? StationId { get; set; }
        public long? OrganizationId { get; set; }
        public List<long>? PermissionIds { get; set; }
    }
}
