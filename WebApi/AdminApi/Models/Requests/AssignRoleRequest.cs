namespace AdminApi.Models.Requests
{
    public class AssignRoleRequest
    {
        public required string PhoneNumber { get; set; }
        public long RoleId { get; set; }
    }
}
