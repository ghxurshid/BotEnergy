using Domain.Entities.BaseEntity;
using Domain.Enums;

namespace Domain.Entities
{ 
    public class ProductEntity : Entity
    { 
        public required string Name { get; set; }

        public ProductType Type { get; set; }

        public UnitType Unit { get; set; }

        public decimal Price { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
