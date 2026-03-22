using AuthApi.Models.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuthApi.Filters
{
    public class LoginValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.ActionArguments["request"] as LoginRequest;

            if (string.IsNullOrEmpty(request?.PhoneNumber))
                context.Result = new BadRequestObjectResult("Phone required");

            if (string.IsNullOrEmpty(request?.Password))
                context.Result = new BadRequestObjectResult("Password required");
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
