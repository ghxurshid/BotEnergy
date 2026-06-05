using Domain.Entities.BaseEntity;

namespace Domain.Entities
{
    /// <summary>Customer rol ↔ permission bog'lanishi (jadval: auth.customer_role_permissions).</summary>
    public class CustomerRolePermissionEntity : Entity
    {
        public long RoleId { get; set; }
        public CustomerRoleEntity? Role { get; set; }

        public long PermissionId { get; set; }
        public PermissionEntity? Permission { get; set; }
    }
}
