using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace CommonConfiguration.ConfigurationExtensions
{
    /// <summary>
    /// Barcha BotEnergy API'lari uchun yagona Serilog konfiguratsiyasi.
    /// Console + File sink (rolling daily). Fayllar service ishlab turgan folder'dan
    /// bitta tepada <c>logs/</c> papkasiga yoziladi — bir nechta servis bir mashinada
    /// turganda hammasining log'lari bir joyga to'planadi.
    /// </summary>
    public static class LoggingExtensions
    {
        /// <summary>
        /// Serilog'ni standart sozlamalar bilan yoqadi: console (qisqa template) + file (kunlik rotation,
        /// 14 kun retention, 100 MB fayl limiti). Fayl yo'li: <c>../logs/{apiName}-YYYYMMDD.log</c>.
        /// Har bir log qatorida <c>Service</c> property servis nomini ko'rsatadi.
        /// </summary>
        public static WebApplicationBuilder AddBotEnergyLogging(this WebApplicationBuilder builder, string apiName)
        {
            // Service ishlab turgan folderdan bitta tepada logs/ papkasi.
            // Production: /home/ubuntu/botenergy/UserApi/ → /home/ubuntu/botenergy/logs/
            // Dev (dotnet run): bin/Debug/net8.0/ → bin/Debug/logs/
            var logDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "logs"));
            Directory.CreateDirectory(logDir);

            const string consoleTemplate =
                "[{Timestamp:HH:mm:ss} {Level:u3}] [{Service}] {Message:lj}{NewLine}{Exception}";

            const string fileTemplate =
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{Service}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

            builder.Host.UseSerilog((context, lc) => lc
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Service", apiName)
                .WriteTo.Console(outputTemplate: consoleTemplate)
                .WriteTo.File(
                    path: Path.Combine(logDir, $"{apiName}-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    fileSizeLimitBytes: 100L * 1024 * 1024,
                    rollOnFileSizeLimit: true,
                    shared: true,
                    outputTemplate: fileTemplate));

            return builder;
        }
    }
}
