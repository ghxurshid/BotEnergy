using Domain.Entities.BaseEntity;

namespace Domain.Entities
{
    public class RolePermissionEntity : Entity
    {
        public long RoleId { get; set; }

        public RoleEntity? Role { get; set; }

        public required string Permission { get; set; }
    }
}
