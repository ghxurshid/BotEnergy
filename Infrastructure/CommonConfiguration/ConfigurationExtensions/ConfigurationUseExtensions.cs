using CommonConfiguration.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Persistence.Context;
using Persistence.Seed;

namespace CommonConfiguration.ConfigurationExtensions
{
    public static class ConfigurationUseExtensions
    {
        /// <summary>
        /// DB yaratadi (agar yo'q bo'lsa), pending migration larni apply qiladi va seed data ni qo'shadi.
        /// </summary>
        public static async Task ApplyMigrationsAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

            try
            {
                logger.LogInformation("Applying database migrations...");
                await context.Database.MigrateAsync();
                logger.LogInformation("Database migrations applied successfully.");

                logger.LogInformation("Seeding data...");
                await DataSeeder.SeedAsync(context);
                logger.LogInformation("Data seeding completed.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to apply migrations or seed data.");
                throw;
            }
        }

        public static IApplicationBuilder UseCustomExceptionMiddleware(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ExceptionMiddleware>();
        }

        /// <summary>
        /// Hosting:UseHttps true bo'lsagina UseHttpsRedirection qo'shadi.
        /// </summary>
        public static IApplicationBuilder UseHttpsIfEnabled(this WebApplication app)
        {
            if (app.Configuration.GetValue<bool>("Hosting:UseHttps"))
                app.UseHttpsRedirection();
            return app;
        }

        /// <summary>
        /// Configuration dan portni o'qib, http yoki https rejimda ishga tushiradi.
        /// Hosting:Ports:{apiName} — port, Hosting:UseHttps — protokol.
        /// </summary>
        public static void RunApi(this WebApplication app, string apiName, int defaultPort)
        {
            var config = app.Configuration;
            var port = config[$"Hosting:Ports:{apiName}"] ?? defaultPort.ToString();
            var scheme = config.GetValue<bool>("Hosting:UseHttps") ? "https" : "http";
            app.Run($"{scheme}://*:{port}");
        }
    }
}
