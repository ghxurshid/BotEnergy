namespace AdminApi.Models.Requests
{
    /// <summary>Platform rol yaratish so'rovi. MerchantId null = Manage (global) rol.</summary>
    public class CreateRoleRequest
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
        public long? MerchantId { get; set; }
        public List<long>? PermissionIds { get; set; }
    }
}
