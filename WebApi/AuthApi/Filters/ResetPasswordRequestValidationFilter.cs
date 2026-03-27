using AuthApi.Models.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuthApi.Filters
{
    public class ResetPasswordRequestValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.ActionArguments["request"] as ResetPasswordRequestRequest;

            if (string.IsNullOrEmpty(request?.PhoneNumber))
                context.Result = new BadRequestObjectResult(new { message = "Telefon raqam kiritilishi shart." });
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
