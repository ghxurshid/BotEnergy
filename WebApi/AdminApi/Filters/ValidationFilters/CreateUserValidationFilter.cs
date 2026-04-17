using AdminApi.Models.Requests;
using CommonConfiguration.Validators;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AdminApi.Filters.ValidationFilters
{
    public class CreateUserValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.ActionArguments["request"] as CreateUserRequest;
            if (request is null) { context.Result = new BadRequestObjectResult(new { message = "So'rov ma'lumotlari noto'g'ri." }); return; }

            if (string.IsNullOrWhiteSpace(request.PhoneId))
            { context.Result = new BadRequestObjectResult(new { message = "PhoneId kiritilishi shart." }); return; }

            if (string.IsNullOrWhiteSpace(request.Mail))
            { context.Result = new BadRequestObjectResult(new { message = "Email kiritilishi shart." }); return; }

            if (!PhoneValidator.IsValid(request.PhoneNumber))
            { context.Result = new BadRequestObjectResult(new { message = PhoneValidator.ErrorMessage }); return; }

            if (request.RoleId <= 0)
            { context.Result = new BadRequestObjectResult(new { message = "Rol ID kiritilishi shart." }); return; }

            if (!request.OrganizationId.HasValue && !request.StationId.HasValue)
            { context.Result = new BadRequestObjectResult(new { message = "OrganizationId yoki StationId dan biri ko'rsatilishi shart." }); return; }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
