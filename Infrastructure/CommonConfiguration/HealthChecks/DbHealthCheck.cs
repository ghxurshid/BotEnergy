using Microsoft.Extensions.Diagnostics.HealthChecks;
using Persistence.Context;

namespace CommonConfiguration.HealthChecks
{
    /// <summary>PostgreSQL ulanishini tekshiradi (CanConnect).</summary>
    public sealed class DbHealthCheck : IHealthCheck
    {
        private readonly AppDbContext _context;

        public DbHealthCheck(AppDbContext context) => _context = context;

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.Database.CanConnectAsync(cancellationToken)
                    ? HealthCheckResult.Healthy("PostgreSQL OK")
                    : HealthCheckResult.Unhealthy("PostgreSQL ulanib bo'lmadi");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("PostgreSQL xatosi", ex);
            }
        }
    }
}
