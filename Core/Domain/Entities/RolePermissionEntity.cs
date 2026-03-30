using Domain.Entities.BaseEntity;

namespace Domain.Entities
{
    public class RolePermissionEntity : Entity
    {
        public long RoleId { get; set; }

        public RoleEntity? Role { get; set; }

        public long PermissionId { get; set; }
        
        public PermissionEntity? Permission { get; set; }
    }
}
