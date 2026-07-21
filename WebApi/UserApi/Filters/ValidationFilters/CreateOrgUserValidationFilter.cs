using CommonConfiguration.Validators;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using UserApi.Models.Requests;

namespace UserApi.Filters.ValidationFilters
{
    public class CreateOrgUserValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.ActionArguments["request"] as CreateOrgUserRequest;
            if (request is null) { context.Result = new BadRequestObjectResult(new { message = "So'rov ma'lumotlari noto'g'ri." }); return; }

            if (string.IsNullOrWhiteSpace(request.PhoneId))
            { context.Result = new BadRequestObjectResult(new { message = "PhoneId kiritilishi shart." }); return; }

            if (string.IsNullOrWhiteSpace(request.Mail))
            { context.Result = new BadRequestObjectResult(new { message = "Email kiritilishi shart." }); return; }

            if (!PhoneValidator.TryNormalize(request.PhoneNumber, out var phone))
            { context.Result = new BadRequestObjectResult(new { message = PhoneValidator.ErrorMessage }); return; }
            request.PhoneNumber = phone;

            if (request.RoleId <= 0)
            { context.Result = new BadRequestObjectResult(new { message = "Rol ID kiritilishi shart." }); return; }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
