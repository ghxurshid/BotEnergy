using CommonConfiguration.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CommonConfiguration.Filters
{
    public class PermissionFilter : IAsyncActionFilter, IOrderedFilter
    {
        public int Order => 1000;

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var hasSkip = context.ActionDescriptor.EndpointMetadata
                .Any(m => m is SkipPermissionCheckAttribute);

            if (hasSkip)
            {
                await next();
                return;
            }

            if (!(context.HttpContext.User.Identity?.IsAuthenticated ?? false))
            {
                context.Result = new UnauthorizedObjectResult(new { message = "Tizimga kirish talab qilinadi." });
                return;
            }

            var requireAttr = context.ActionDescriptor.EndpointMetadata
                .OfType<RequirePermissionAttribute>()
                .FirstOrDefault();

            string requiredPermission;

            if (requireAttr != null)
            {
                requiredPermission = requireAttr.Permission;
            }
            else
            {
                var controller = context.RouteData.Values["controller"]?.ToString();
                var action = context.RouteData.Values["action"]?.ToString();
                requiredPermission = $"{controller}.{action}";
            }

            var userPermissions = context.HttpContext.User.Claims
                .Where(c => c.Type == "Permission")
                .Select(c => c.Value)
                .ToHashSet();

            if (!userPermissions.Contains(requiredPermission))
            {
                context.Result = new ObjectResult(new
                {
                    message = $"Bu amalni bajarish uchun '{requiredPermission}' ruxsati talab qilinadi."
                })
                {
                    StatusCode = 403
                };
                return;
            }

            await next();
        }
    }
}
