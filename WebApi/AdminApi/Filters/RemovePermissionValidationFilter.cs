using AdminApi.Models.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AdminApi.Filters
{
    public class RemovePermissionValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.ActionArguments["request"] as RemovePermissionRequest;

            if (request?.RoleId <= 0)
                context.Result = new BadRequestObjectResult(new { message = "Rol ID kiritilishi shart." });

            if (string.IsNullOrWhiteSpace(request?.Permission))
                context.Result = new BadRequestObjectResult(new { message = "Permission nomi kiritilishi shart." });
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
