using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Persistence.Context
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

            // EF tools ishlayotganda solution root dan config topishga harakat qiladi
            var possiblePaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "ConfigurationFile"),
                Path.Combine(Directory.GetCurrentDirectory(), "ConfigurationFile"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "CommonConfiguration", "ConfigurationFile"),
            };

            var configBasePath = possiblePaths.FirstOrDefault(Directory.Exists)
                ?? Path.Combine(AppContext.BaseDirectory, "ConfigurationFile");

            var configuration = new ConfigurationBuilder()
                .SetBasePath(configBasePath)
                .AddJsonFile("Configuration.json", optional: true)
                .AddJsonFile($"Configuration.{environment}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection");

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public"));

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
