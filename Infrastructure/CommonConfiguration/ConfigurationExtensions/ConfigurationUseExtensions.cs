using CommonConfiguration.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
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
            // Production'da migratsiya alohida Migrator konteyneri orqali bir marta qo'llanadi
            // (deploy pipeline'ida servislardan OLDIN) — 7 API'ning parallel MigrateAsync
            // poygasi va noto'g'ri migratsiyaning jimgina prodga chiqishi shu bilan yo'qoladi.
            // Migrate:AutoApply=false bo'lsa bu metod hech narsa qilmaydi.
            if (!app.Configuration.GetValue("Migrate:AutoApply", true))
            {
                app.Services.GetRequiredService<ILogger<AppDbContext>>()
                    .LogInformation("Migrate:AutoApply=false — migratsiya bu jarayonda qo'llanmaydi (Migrator mas'ul).");
                return;
            }

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
        /// Originlar Cors:AllowedOrigins (string massiv) dan olinadi. Ro'yxat bo'sh bo'lsa
        /// yoki ichida "*" bo'lsa — muhitdan qat'i nazar har qanday origin qabul qilinadi
        /// (simulyatorlar file://, localhost va nginx ostidan ochiladi).
        /// DIQQAT: CORS server tomon himoyasi emas — u faqat brauzerga ta'sir qiladi, API
        /// baribir curl va mobil ilova uchun ochiq. Himoya JWT + PermissionFilter zimmasida.
        /// </summary>
        // UseSimulatorCors log'i uchun — qaysi qiymat KUCHGA KIRGANINI ko'rsatadi.
        // Env var (Cors__AllowedOrigins__0=...) json'ni override qiladi, shuning uchun
        // fayldagi qiymatga qarab xulosa qilib bo'lmaydi.
        private static string[] _corsOrigins = Array.Empty<string>();
        private static bool _corsAllowAny = true;

        public static IServiceCollection AddSimulatorCors(this IServiceCollection services, IConfiguration config)
        {
            var configured = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

            // Tozalash: bo'sh qatorlar, "Env_" placeholder'lari va atrofdagi bo'shliqlar
            // hisobga olinmaydi; oxiridagi "/" kesiladi. Brauzer Origin'ni HECH QACHON
            // oxirida "/" bilan yubormaydi — "https://x.uz/" deb yozilgan qiymat jimgina
            // hech bir originga mos kelmay, hamma so'rovni blokda qoldirardi.
            var origins = configured
                .Where(o => !string.IsNullOrWhiteSpace(o) && !o.StartsWith("Env_", StringComparison.Ordinal))
                .Select(o => o.Trim().TrimEnd('/'))
                .ToArray();

            // Cors:AllowAnyOrigin — ro'yxatdan USTUN turadigan aniq bayroq. U bazaviy
            // Configuration.json'da yashaydi (optional:false — har doim yuklanadi), ya'ni
            // muhit fayli yuklanmasa ham, serverdagi Cors__AllowedOrigins__0 env var'i
            // qanday qiymat tutsa ham, ochiqlik kafolatlanadi. Domen aniq bo'lgach shu
            // bayroqni false qiling va ro'yxatga tayaning.
            var allowAnyOrigin = config.GetValue("Cors:AllowAnyOrigin", false)
                                 || origins.Length == 0
                                 || origins.Contains("*");
            _corsOrigins = origins;
            _corsAllowAny = allowAnyOrigin;

            services.AddCors(options =>
            {
                options.AddPolicy(SimulatorCorsPolicy, policy =>
                {
                    // AllowCredentials() bilan AllowAnyOrigin() birga ishlamaydi (runtime'da exception
                    // otadi), shuning uchun "hammasi ochiq" holatida origin aks ettiriladi.
                    if (allowAnyOrigin)
                        policy.SetIsOriginAllowed(_ => true);
                    else
                        policy.WithOrigins(origins);

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
        {
            // CORS jimgina bloklaydi: brauzer konsolida "Failed to fetch", serverda esa
            // hech qanday xato yo'q. Shuning uchun samarali qiymat startda yoziladi.
            var logger = app.ApplicationServices.GetService<ILoggerFactory>()?.CreateLogger("BotEnergy.Cors");
            if (_corsAllowAny)
                logger?.LogInformation("CORS: har qanday origin ochiq (Cors:AllowedOrigins bo'sh yoki \"*\")");
            else
                logger?.LogInformation("CORS: faqat shu originlar ruxsat etilgan: {Origins}", string.Join(", ", _corsOrigins));

            return app.UseCors(SimulatorCorsPolicy);
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
        /// Liveness va readiness'ni ajratib map qiladi:
        /// <list type="bullet">
        /// <item><c>/health/live</c> — hech qanday check ishlatmaydi. "Jarayon javob beryaptimi".
        /// deploy.sh restartdan keyin shu endpoint'ni kutadi (health gate).</item>
        /// <item><c>/health/ready</c> — "ready" tegli check'lar (DB, Redis). YARP active health
        /// check shu endpoint'ga qaraydi: bog'liqlik yiqilsa trafik boshqa replikaga o'tadi,
        /// lekin servis restart qilinmaydi.</item>
        /// <item><c>/health</c> — orqaga moslik uchun, /health/ready bilan bir xil.</item>
        /// </list>
        /// </summary>
        public static WebApplication MapBotEnergyHealthChecks(this WebApplication app)
        {
            app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
            app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
            app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
            return app;
        }

        /// <summary>
        /// Swagger'ni faqat <c>Swagger:Enabled</c> true bo'lsa yoqadi (Production'da default false).
        /// Reverse proxy ortida ishlaganda <c>servers</c> URL'i gateway prefiksiga moslanadi —
        /// aks holda Swagger UI'dagi "Try it out" 404 qaytaradi.
        /// </summary>
        public static WebApplication UseSwaggerIfEnabled(this WebApplication app)
        {
            if (!app.Configuration.GetValue("Swagger:Enabled", true))
                return app;

            app.UseSwagger(options =>
            {
                options.PreSerializeFilters.Add((document, request) =>
                {
                    // Gateway YARP transform orqali yuboradi: X-Forwarded-Prefix: /auth
                    // Gateway faqat prefiksni olib tashlaydi, boshqa o'zgartirish qilmaydi —
                    // shuning uchun ommaviy yo'l = prefiks + spec'dagi yo'l. "Try it out"
                    // shu sababli to'g'ri manzilga uradi: /auth + /api/PlatformAuth/Login.
                    var prefix = request.Headers["X-Forwarded-Prefix"].FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(prefix))
                        document.Servers = new List<OpenApiServer> { new() { Url = prefix } };
                });
            });
            app.UseSwaggerUI(options =>
            {
                // NISBIY yo'l — boshida "/" YO'Q. Swagger UI uni joriy sahifaga nisbatan hal qiladi:
                //   to'g'ridan-to'g'ri  /swagger/index.html       → /swagger/v1/swagger.json
                //   gateway ortida      /auth/swagger/index.html  → /auth/swagger/v1/swagger.json
                // Absolyut "/swagger/v1/swagger.json" bo'lsa gateway ortida 404 bo'lardi:
                // gateway'da prefiksiz "/swagger" marshruti yo'q.
                options.SwaggerEndpoint("v1/swagger.json", "v1");
            });
            return app;
        }

        /// <summary>
        /// Nginx/YARP ortida haqiqiy klient IP va sxemani tiklaydi.
        /// Busiz <c>RemoteIpAddress</c> proxy IP'si bo'lib qoladi va IP bo'yicha rate limiting
        /// butunlay buziladi — barcha foydalanuvchilar bitta partition'ga tushadi.
        /// </summary>
        public static WebApplication UseProxyForwardedHeaders(this WebApplication app)
        {
            app.UseForwardedHeaders();
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
