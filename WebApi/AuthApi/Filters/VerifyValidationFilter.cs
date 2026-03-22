using AuthApi.Models.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuthApi.Filters
{
    public class VerifyValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.ActionArguments["request"] as VerifyRequest;

            if (string.IsNullOrEmpty(request?.OtpCode))
                context.Result = new BadRequestObjectResult("OTP required");
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
