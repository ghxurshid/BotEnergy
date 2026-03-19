using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Dtos
{
    public class RegisterDto
    {
        public required string PhoneId { get; set; }
        public required string Mail { get; set; }
        public required string PhoneNumber { get; set; }
        public required string Password { get; set; }
    }

    public class RegisterResultDto
    {
        public required string ResultMessage { get; set; }         
    }
}
