using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Dtos
{
    public class LoginDto
    {
        public required string PhoneNumber { get; set; }      
        public required string Password { get; set; }
    }

    public class LoginResultDto
    { 
        public required string AccessToken { get; set; }
        public required string RefreshToken { get; set; }
        public required DateTime AccessTokenExpiration { get; set; }
    }
}
