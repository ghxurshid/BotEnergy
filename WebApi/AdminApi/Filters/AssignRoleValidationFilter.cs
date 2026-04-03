using AdminApi.Models.Requests;
using CommonConfiguration.Validators;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AdminApi.Filters
{
    public class AssignRoleValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.ActionArguments["request"] as AssignRoleRequest;
            if (request is null) { context.Result = new BadRequestObjectResult(new { message = "So'rov ma'lumotlari noto'g'ri." }); return; }

            if (!PhoneValidator.IsValid(request.PhoneNumber))
            { context.Result = new BadRequestObjectResult(new { message = PhoneValidator.ErrorMessage }); return; }

            if (request.RoleId <= 0)
            { context.Result = new BadRequestObjectResult(new { message = "Rol ID kiritilishi shart." }); return; }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
