namespace UserApi.Models.Requests
{
    /// <summary>
    /// Corporate admin o'z tashkiloti uchun rol yaratadi.
    /// OrganizationId so'rovda emas — token scope'idan olinadi.
    /// </summary>
    public class CreateOrgRoleRequest
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
        public List<long>? PermissionIds { get; set; }
    }
}
