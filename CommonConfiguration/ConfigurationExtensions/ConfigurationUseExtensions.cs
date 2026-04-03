using CommonConfiguration.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

namespace CommonConfiguration.ConfigurationExtensions
{
    public static class ConfigurationUseExtensions
    {
        public static IServiceCollection AddDBConfigurationExtension()
        {
            return default!;
        }

        public static IApplicationBuilder UseCustomExceptionMiddleware(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ExceptionMiddleware>();
        }
    }
}
