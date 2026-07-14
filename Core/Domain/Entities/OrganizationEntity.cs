using Domain.Entities.BaseEntity;
using Domain.Interfaces;

namespace Domain.Entities
{
    public class OrganizationEntity : Entity, IHasPhoneNumber
    {
        public required string Name { get; set; }

        public required string Inn { get; set; }

        public required string Address { get; set; }

        public required string PhoneNumber { get; set; }

        public decimal Balance { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public ICollection<CustomerUserEntity>? CustomerUsers { get; set; }

        public ICollection<CustomerRoleEntity>? Roles { get; set; }
    }
}
