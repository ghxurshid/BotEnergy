using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Dtos.Base
{
    public class Error
    {
        public string ErrorMessage { get; set; } = string.Empty;
        public int Code { get; set; }
    }
    public class GenericDto<TResult>
    {
        public bool IsSuccess { get; set; }
        public TResult? Result { get; set; }
        public Error? ErrorObj { get; set; }

        public static GenericDto<TResult> Success(TResult? result)
        {
            return new GenericDto<TResult> { IsSuccess = true, Result = result };
        }

        public static GenericDto<TResult> Error(int errorCode, string errorMessage)
        {
            return new GenericDto<TResult> { IsSuccess = false, ErrorObj = new Error { Code = errorCode, ErrorMessage = errorMessage } };
        }
    }
}
