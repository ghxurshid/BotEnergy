using Domain.Entities.BaseEntity;
using Domain.Enums;

namespace Domain.Entities
{
    public abstract class RoleEntity : Entity
    {
        public abstract RoleType RoleType { get; }

        public required string Name { get; set; }

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<RolePermissionEntity>? RolePermissions { get; set; }

        public ICollection<UserRoleEntity>? UserRoles { get; set; }
    }
}
