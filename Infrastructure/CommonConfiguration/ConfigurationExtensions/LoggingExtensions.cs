using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace CommonConfiguration.ConfigurationExtensions
{
    /// <summary>
    /// Barcha BotEnergy API'lari uchun yagona Serilog konfiguratsiyasi.
    ///
    /// Ikki rejim (<c>Observability:ConsoleJsonLogs</c> bilan tanlanadi):
    /// <list type="bullet">
    /// <item><b>false</b> (Development) — o'qishga qulay console + kunlik fayl sink.</item>
    /// <item><b>true</b> (Production/Docker) — faqat stdout'ga compact JSON. Konteynerda
    /// fayl sink ma'nosiz: konteyner o'chganda loglar yo'qoladi. Promtail stdout'ni yig'ib
    /// Loki'ga uzatadi.</item>
    /// </list>
    ///
    /// Har yozuvda <c>Service</c>, <c>TraceId</c> va <c>SpanId</c> bo'ladi — Loki'dagi logdan
    /// Tempo'dagi trace'ga bir bosishda o'tish shu maydonlarga tayanadi.
    /// </summary>
    public static class LoggingExtensions
    {
        public static WebApplicationBuilder AddBotEnergyLogging(this WebApplicationBuilder builder, string apiName)
        {
            // Logging konfiguratsiyasi Configuration.json'dan o'qiladi. AddCommonConfiguration()
            // Program.cs'da keyinroq chaqirilishi mumkin, shuning uchun bu yerda mustaqil o'qiymiz.
            var config = ConfigurationServices.CommonConfiguration.GetConfiguration();
            var jsonLogs = config.GetValue("Observability:ConsoleJsonLogs", false);

            const string consoleTemplate =
                "[{Timestamp:HH:mm:ss} {Level:u3}] [{Service}] {Message:lj}{NewLine}{Exception}";

            const string fileTemplate =
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{Service}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

            builder.Host.UseSerilog((context, lc) =>
            {
                lc
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("Service", apiName)
                    .Enrich.With<TraceContextEnricher>();

                if (jsonLogs)
                {
                    // Docker: bitta oqim, strukturali. Fayl sink yo'q.
                    lc.WriteTo.Console(new CompactJsonFormatter());
                }
                else
                {
                    // Service ishlab turgan folderdan bitta tepada logs/ papkasi.
                    // Dev (dotnet run): bin/Debug/net8.0/ → bin/Debug/logs/
                    var logDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "logs"));
                    Directory.CreateDirectory(logDir);

                    lc.WriteTo.Console(outputTemplate: consoleTemplate)
                      .WriteTo.File(
                          path: Path.Combine(logDir, $"{apiName}-.log"),
                          rollingInterval: RollingInterval.Day,
                          retainedFileCountLimit: 14,
                          fileSizeLimitBytes: 100L * 1024 * 1024,
                          rollOnFileSizeLimit: true,
                          shared: true,
                          outputTemplate: fileTemplate);
                }
            });

            return builder;
        }
    }

    /// <summary>
    /// Joriy <see cref="Activity"/> dan TraceId/SpanId ni log yozuviga qo'shadi.
    /// Tracing o'chirilgan bo'lsa Activity.Current null bo'ladi va hech narsa qo'shilmaydi.
    /// </summary>
    public sealed class TraceContextEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var activity = Activity.Current;
            if (activity is null) return;

            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString()));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SpanId", activity.SpanId.ToString()));
        }
    }
}
