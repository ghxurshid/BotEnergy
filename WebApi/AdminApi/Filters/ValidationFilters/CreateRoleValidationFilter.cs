using AdminApi.Models.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AdminApi.Filters.ValidationFilters
{
    public class CreateRoleValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.ActionArguments["request"] as CreateRoleRequest;

            if (string.IsNullOrWhiteSpace(request?.Name))
                context.Result = new BadRequestObjectResult(new { message = "Rol nomi kiritilishi shart." });
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
