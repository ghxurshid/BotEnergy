using Domain.Enums;

namespace Domain.Entities
{
    public class LegalRoleEntity : RoleEntity
    {
        public override RoleType RoleType => RoleType.LegalRole;

        public long OrganizationId { get; set; }
        public OrganizationEntity? Organization { get; set; }
    }
}
