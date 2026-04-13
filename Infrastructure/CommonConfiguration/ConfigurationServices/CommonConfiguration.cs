using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommonConfiguration.ConfigurationServices
{
    public static class CommonConfiguration
    {
        public static IConfiguration GetConfiguration()
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
            var configBasePath = Path.Combine(AppContext.BaseDirectory, "ConfigurationFile");

            var builder = new ConfigurationBuilder()
                .SetBasePath(configBasePath)
                .AddJsonFile("Configuration.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"Configuration.{environment}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            return builder.Build();
        }

        public static ConfigurationManager AddCommonConfiguration(this ConfigurationManager manager)
        {
            var config = GetConfiguration();

            // Strongly typed options
            //services.Configure<AppSettings>(config.GetSection("AppSettings"));
            //services.Configure<DatabaseSettings>(config.GetSection("DatabaseSettings"));

            // Agar kerak bo‘lsa IConfiguration ni ham register qilamiz
            manager.AddConfiguration(config);

            return manager;
        }
    }
}
