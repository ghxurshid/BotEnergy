using AuthApi.Models.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuthApi.Filters
{
    public class ResetPasswordSetValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.ActionArguments["request"] as ResetPasswordSetRequest;

            if (string.IsNullOrEmpty(request?.PhoneNumber))
                context.Result = new BadRequestObjectResult(new { message = "Telefon raqam kiritilishi shart." });

            if (request?.NewPassword.Length < 6)
                context.Result = new BadRequestObjectResult(new { message = "Yangi parol kamida 6 ta belgidan iborat bo'lishi kerak." });
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
