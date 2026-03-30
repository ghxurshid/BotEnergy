using Domain.Enums;

namespace Domain.Entities
{
    public class LegalUserEntity : UserEntity
    {
        public override UserType UserType => UserType.LegalEntity;

        public long? OrganizationId { get; set; }
        public OrganizationEntity? Organization { get; set; }
    }
}
