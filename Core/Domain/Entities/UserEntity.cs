using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    public class UserEntity
    {
        public Guid Id { get; set; }

        public string PhoneNumber { get; set; }

        public string PhoneId { get; set; }

        public string Gmail { get; set; }

        public decimal Balance { get; set; }

        public bool IsBlocked { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
