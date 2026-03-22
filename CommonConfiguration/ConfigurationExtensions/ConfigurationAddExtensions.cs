using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Persistence.Context;
using System;

namespace CommonConfiguration.ConfigurationExtensions
{
    public static class ConfigurationAddExtensions
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
        {
            services.AddDbContext<Context>(options =>
                options.UseNpgsql(config.GetConnectionString("DefaultConnection")));

            return services;
        }
    }
}
