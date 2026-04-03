using AuthApi.Models.Requests;
using CommonConfiguration.Validators;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuthApi.Filters
{
    public class RegisterValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.ActionArguments["request"] as RegisterRequest;
            if (request is null) { context.Result = new BadRequestObjectResult(new { message = "So'rov ma'lumotlari noto'g'ri." }); return; }

            if (!PhoneValidator.IsValid(request.PhoneNumber))
            { context.Result = new BadRequestObjectResult(new { message = PhoneValidator.ErrorMessage }); return; }

            if (string.IsNullOrWhiteSpace(request.Mail))
            { context.Result = new BadRequestObjectResult(new { message = "Email kiritilishi shart." }); return; }

            if (!request.Mail.Contains('@'))
            { context.Result = new BadRequestObjectResult(new { message = "Email formati noto'g'ri." }); return; }

            if (string.IsNullOrWhiteSpace(request.PhoneId))
            { context.Result = new BadRequestObjectResult(new { message = "Qurilma identifikatori kiritilishi shart." }); return; }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
