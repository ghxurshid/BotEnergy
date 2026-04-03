using AuthApi.Models.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuthApi.Filters.ValidationFilters
{
    public class RefreshTokenValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.ActionArguments["request"] as RefreshTokenRequest;

            if (string.IsNullOrEmpty(request?.RefreshToken))
                context.Result = new BadRequestObjectResult("Refresh token required"); 
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
