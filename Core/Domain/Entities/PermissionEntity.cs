using Domain.Entities.BaseEntity;

namespace Domain.Entities
{
    public class PermissionEntity : Entity
    {
        public required string Name { get; set; }

        public string? Description { get; set; }

        public ICollection<PlatformRolePermissionEntity>? PlatformRolePermissions { get; set; }
        public ICollection<CustomerRolePermissionEntity>? CustomerRolePermissions { get; set; }
    }
}
