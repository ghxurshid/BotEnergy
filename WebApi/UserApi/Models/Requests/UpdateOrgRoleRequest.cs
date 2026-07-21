namespace UserApi.Models.Requests
{
    public class UpdateOrgRoleRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
        public List<long>? PermissionIds { get; set; }
    }
}
