namespace AdminApi.Models.Requests
{
    /// <summary>Corporate rol yaratish so'rovi. OrganizationId Manage uchun majburiy,
    /// Corporate admin uchun e'tiborsiz (o'z tashkiloti olinadi).</summary>
    public class CreateCorporateRoleRequest
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
        public long? OrganizationId { get; set; }
        public List<long>? PermissionIds { get; set; }
    }
}
