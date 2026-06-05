using Domain.Entities.BaseEntity;

namespace Domain.Entities
{
    /// <summary>Platform rol ↔ permission bog'lanishi (jadval: auth.platform_role_permissions).</summary>
    public class PlatformRolePermissionEntity : Entity
    {
        public long RoleId { get; set; }
        public PlatformRoleEntity? Role { get; set; }

        public long PermissionId { get; set; }
        public PermissionEntity? Permission { get; set; }
    }
}
