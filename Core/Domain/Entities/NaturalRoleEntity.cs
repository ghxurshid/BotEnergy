using Domain.Enums;

namespace Domain.Entities
{
    public class NaturalRoleEntity : RoleEntity
    {
        public override RoleType RoleType => RoleType.NaturalRole;
    }
}
