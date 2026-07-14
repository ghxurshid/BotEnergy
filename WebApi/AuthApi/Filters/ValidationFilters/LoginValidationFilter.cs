using AuthApi.Models.Requests;
using CommonConfiguration.Validators;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuthApi.Filters.ValidationFilters
{
    public class LoginValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.ActionArguments["request"] as LoginRequest;
            if (request is null) { context.Result = new BadRequestObjectResult(new { message = "So'rov ma'lumotlari noto'g'ri." }); return; }

            if (!PhoneValidator.TryNormalize(request.PhoneNumber, out var phone))
            { context.Result = new BadRequestObjectResult(new { message = PhoneValidator.ErrorMessage }); return; }
            request.PhoneNumber = phone;

            if (string.IsNullOrWhiteSpace(request.Password))
            { context.Result = new BadRequestObjectResult(new { message = "Parol kiritilishi shart." }); return; }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
