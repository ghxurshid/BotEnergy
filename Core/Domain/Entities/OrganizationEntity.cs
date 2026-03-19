using Domain.Entities.BaseEntity;

namespace Domain.Entities
{
    public class OrganizationEntity : Entity
    {
        public required string Name { get; set; }

        public string? Inn { get; set; }

        public string? Address { get; set; }

        public string? PhoneNumber { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
