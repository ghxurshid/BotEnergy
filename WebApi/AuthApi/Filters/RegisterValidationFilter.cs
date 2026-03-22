using AuthApi.Models.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuthApi.Filters
{
    public class RegisterValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.ActionArguments["request"] as RegisterRequest;

            if (string.IsNullOrEmpty(request?.PhoneNumber))
                context.Result = new BadRequestObjectResult("Phone required");

            if (request?.Password.Length < 6)
                context.Result = new BadRequestObjectResult("Weak password");
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
