using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Persistence.Context;
using Persistence.Seed;

namespace CommonConfiguration.ConfigurationExtensions
{
    public static class DataSeederExtensions
    {
        public static async Task SeedDataAsync(this IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await DataSeeder.SeedAsync(context);
        }
    }
}
