using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Dtos
{
    public class RefreshTokenDto
    { 
        public required string RefreshToken { get; set; }
    }

    public class RefreshTokenResultDto
    {
        public required string ResultMessage { get; set; }
        public required string AccessToken { get; set; }
        public required string RefreshToken { get; set; }
        public required DateTime AccessTokenExpiration { get; set; }
    }
}
