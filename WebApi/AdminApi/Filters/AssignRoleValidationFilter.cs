using AdminApi.Models.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AdminApi.Filters
{
    public class AssignRoleValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.ActionArguments["request"] as AssignRoleRequest;

            if (string.IsNullOrWhiteSpace(request?.PhoneNumber))
                context.Result = new BadRequestObjectResult(new { message = "Telefon raqam kiritilishi shart." });

            if (request?.RoleId <= 0)
                context.Result = new BadRequestObjectResult(new { message = "Rol ID kiritilishi shart." });
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
