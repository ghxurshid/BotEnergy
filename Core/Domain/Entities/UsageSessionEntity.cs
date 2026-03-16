using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    public class UsageSessionEntity
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public string DeviceId { get; set; }

        public string ProductType { get; set; }

        public decimal Quantity { get; set; }

        public decimal Price { get; set; }

        public DateTime StartedAt { get; set; }

        public DateTime? EndedAt { get; set; }
    }
}
