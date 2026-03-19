using Domain.Entities.BaseEntity;

namespace Domain.Entities
{
    public class StationEntity : Entity
    {
        public required string Name { get; set; }

        public string? Location { get; set; }

        public long OrganizationId { get; set; }

        public OrganizationEntity? Organization { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
