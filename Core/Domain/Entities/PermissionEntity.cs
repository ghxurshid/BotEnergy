using Domain.Entities.BaseEntity;
using Domain.Enums;

namespace Domain.Entities
{ 
    public class PermissionEntity : Entity
    { 
        public required string Name { get; set; }
 
        public string? Description { get; set; } 

         public ICollection<RolePermissionEntity>? RolePermissions { get; set; }
    }
}
