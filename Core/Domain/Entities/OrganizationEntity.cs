using Domain.Entities.BaseEntity;

namespace Domain.Entities
{
    public class OrganizationEntity : Entity
    {
        public required string Name { get; set; }

        public required string Inn { get; set; }

        public required string Address { get; set; }

        public required string PhoneNumber { get; set; }

        public decimal Balance { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public ICollection<LegalUserEntity>? LegalUsers { get; set; }

        public ICollection<LegalRoleEntity>? Roles { get; set; }
    }
}
