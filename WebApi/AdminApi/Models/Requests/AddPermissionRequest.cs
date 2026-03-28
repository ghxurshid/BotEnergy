namespace AdminApi.Models.Requests
{
    public class AddPermissionRequest
    {
        public long RoleId { get; set; }
        public required string Permission { get; set; }
    }
}
