using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Dtos.Base
{
    public class Error
    {
        public string ErrorMessage { get; set; }
        public int Code { get; set; }
    }
    public class GenericDto<TResult>
    {
        public bool IsSuccess { get; set; }
        public TResult? Result { get; set; }
        public Error? Error { get; set; }
    }
}
