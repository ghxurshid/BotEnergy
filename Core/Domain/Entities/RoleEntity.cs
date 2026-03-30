using Domain.Entities.BaseEntity;

namespace Domain.Entities
{
    public class RoleEntity : Entity
    {
        public required string Name { get; set; }

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true; 

        public ICollection<RolePermissionEntity>? RolePermissions { get; set; }

        public ICollection<UserRoleEntity>? UserRoles { get; set; }
    }
}
