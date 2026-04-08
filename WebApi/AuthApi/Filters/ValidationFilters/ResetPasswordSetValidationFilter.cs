using AuthApi.Models.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuthApi.Filters.ValidationFilters
{
    public class ResetPasswordSetValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.ActionArguments["request"] as ResetPasswordSetRequest;
            if (request is null) { context.Result = new BadRequestObjectResult(new { message = "So'rov ma'lumotlari noto'g'ri." }); return; }

            if (request.UserId <= 0)
            { context.Result = new BadRequestObjectResult(new { message = "UserId noto'g'ri." }); return; }

            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            { context.Result = new BadRequestObjectResult(new { message = "Yangi parol kamida 6 ta belgidan iborat bo'lishi kerak." }); return; }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
