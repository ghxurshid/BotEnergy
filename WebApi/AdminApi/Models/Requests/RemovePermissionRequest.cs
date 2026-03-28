namespace AdminApi.Models.Requests
{
    public class RemovePermissionRequest
    {
        public long RoleId { get; set; }
        public required string Permission { get; set; }
    }
}
