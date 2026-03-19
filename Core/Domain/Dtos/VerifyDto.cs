using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Dtos
{
    public class VerifyDto
    {
        public required long UserId { get; set; }
        public required string OtpCode { get; set; }
    }

    public class VerifyResultDto
    {
        public required string ResultMessage { get; set; }
    }
}
