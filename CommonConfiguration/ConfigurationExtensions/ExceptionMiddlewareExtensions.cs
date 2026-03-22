using CommonConfiguration.Middlewares;
using Microsoft.AspNetCore.Builder;

namespace CommonConfiguration.ConfigurationExtensions
{
    public static class ExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseCustomExceptionMiddleware(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ExceptionMiddleware>();
        }
    }
}
