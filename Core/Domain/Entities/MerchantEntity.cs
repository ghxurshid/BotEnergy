using Domain.Entities.BaseEntity;

namespace Domain.Entities
{
    public class MerchantEntity : Entity
    {
        public required string PhoneNumber { get; set; }

        public required string Inn { get; set; }

        public required string BankAccount { get; set; }

        public required string CompanyName { get; set; }

        public bool IsActive { get; set; } = true; 

        public ICollection<StationEntity>? Stations { get; set; }
    }
}
