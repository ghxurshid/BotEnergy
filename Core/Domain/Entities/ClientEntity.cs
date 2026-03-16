using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    public class ClientEntity
    {
        public Guid Id { get; set; }

        public string PhoneNumber { get; set; }

        public string Inn { get; set; }

        public string BankAccount { get; set; }

        public string CompanyName { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
