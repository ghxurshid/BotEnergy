using Domain.Entities.BaseEntity;
using Domain.Enums;

namespace Domain.Entities
{
    public class RolePermissionEntity : Entity
    {
        public long RoleId { get; set; }

        public RoleEntity? Role { get; set; }

        public Permission Permission { get; set; }
    }
}
