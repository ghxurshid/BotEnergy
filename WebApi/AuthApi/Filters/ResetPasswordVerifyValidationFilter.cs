using AuthApi.Models.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuthApi.Filters
{
    public class ResetPasswordVerifyValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.ActionArguments["request"] as ResetPasswordVerifyRequest;

            if (string.IsNullOrEmpty(request?.PhoneNumber))
                context.Result = new BadRequestObjectResult(new { message = "Telefon raqam kiritilishi shart." });

            if (string.IsNullOrEmpty(request?.OtpCode))
                context.Result = new BadRequestObjectResult(new { message = "OTP kod kiritilishi shart." });
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
