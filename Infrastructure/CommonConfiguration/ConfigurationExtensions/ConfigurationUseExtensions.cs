using CommonConfiguration.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Persistence.Context;
using Persistence.Seed;
using Microsoft.AspNetCore.Cors.Infrastructure;

namespace CommonConfiguration.ConfigurationExtensions
{
    public static class ConfigurationUseExtensions
    {
        /// <summary>
        /// DB yaratadi (agar yo'q bo'lsa), pending migration larni apply qiladi va seed data ni qo'shadi.
        /// </summary>
        /// <summary>Advisory lock kaliti — barcha BotEnergy API'lari uchun bitta.</summary>
        private const long MigrationLockKey = 727_272_001;

        public static async Task ApplyMigrationsAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

            // Deploy'da 7 API bir vaqtda ko'tariladi — advisory lock bilan migratsiya/seed
            // faqat bitta processda ishlaydi, qolganlari kutib turadi.
            // DB hali yaratilmagan bo'lsa (birinchi boot) connection ochilmaydi — lock'siz davom etamiz,
            // MigrateAsync o'zi DB yaratadi.
            var lockTaken = false;
            try
            {
                await context.Database.OpenConnectionAsync();
                await context.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_lock({MigrationLockKey})");
                lockTaken = true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Migration lock olinmadi (DB hali yo'q bo'lishi mumkin) — lock'siz davom etiladi.");
            }

            try
            {
                logger.LogInformation("Applying database migrations...");
                await context.Database.MigrateAsync();
                logger.LogInformation("Database migrations applied successfully.");

                logger.LogInformation("Seeding data...");
                // ResolveSecret: "Env_*" placeholder (env var berilmagan) parol sifatida o'tib ketmasin.
                var seedAdminPassword = ConfigurationAddExtensions.ResolveSecret(app.Configuration, "Seed:AdminPassword");
                await DataSeeder.SeedAsync(
                    context,
                    adminPassword: seedAdminPassword,
                    isDevelopment: app.Environment.IsDevelopment());

                if (!app.Environment.IsDevelopment() && seedAdminPassword is null)
                {
                    logger.LogWarning(
                        "Seed:AdminPassword berilmagan — default admin yaratilmadi. " +
                        "Kerak bo'lsa env var bering: Seed__AdminPassword.");
                }
                logger.LogInformation("Data seeding completed.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to apply migrations or seed data.");
                throw;
            }
            finally
            {
                if (lockTaken)
                {
                    await context.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_unlock({MigrationLockKey})");
                    await context.Database.CloseConnectionAsync();
                }
            }
        }

        public static IApplicationBuilder UseCustomExceptionMiddleware(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ExceptionMiddleware>();
        }

        public const string SimulatorCorsPolicy = "BotEnergySimulatorCors";

        /// <summary>
        /// Brauzerda ishlaydigan klientlar (simulyatorlar, admin panel) uchun CORS.
        /// Originlar Cors:AllowedOrigins (string massiv) dan olinadi.
        /// Ro'yxat berilmagan bo'lsa: Development'da hamma origin ochiq (simulyatorlar ishlashi uchun),
        /// Production'da hech qanday cross-origin ruxsat berilmaydi (native app'larga CORS ta'sir qilmaydi).
        /// </summary>
        public static IServiceCollection AddSimulatorCors(this IServiceCollection services, IConfiguration config)
        {
            var origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
            var isProduction = string.Equals(env, "Production", StringComparison.OrdinalIgnoreCase);

            services.AddCors(options =>
            {
                options.AddPolicy(SimulatorCorsPolicy, policy =>
                {
                    if (origins.Length > 0)
                        policy.WithOrigins(origins);
                    else if (!isProduction)
                        policy.SetIsOriginAllowed(_ => true); // faqat dev — simulyatorlar file:// yoki localhost'dan ochiladi
                    // prod + ro'yxat yo'q → hech bir origin ruxsat etilmaydi

                    policy
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials()
                        .WithExposedHeaders("Idempotent-Replay")
                        // Brauzer preflight (OPTIONS) javobini shu muddatga cache qiladi —
                        // har GET/PUT oldidan OPTIONS qayta yuborilmaydi (Chrome maks 2 soatgacha qisadi).
                        .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
                });
            });
            return services;
        }

        public static IApplicationBuilder UseSimulatorCors(this IApplicationBuilder app)
            => app.UseCors(SimulatorCorsPolicy);

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
