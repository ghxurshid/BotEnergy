using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{ 
    public class ProductEntity
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public ProductType Type { get; set; }

        public UnitType Unit { get; set; }

        public decimal Price { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
