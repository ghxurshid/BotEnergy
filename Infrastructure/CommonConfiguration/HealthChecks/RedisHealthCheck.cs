using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace CommonConfiguration.HealthChecks
{
    /// <summary>
    /// Redis ulanishini tekshiradi. Redis ro'yxatdan o'tmagan API'larda "not configured" (Healthy).
    /// Redis yo'qligi app'ni yiqitmaydi (fallback bor) — shuning uchun Unhealthy emas, Degraded.
    /// </summary>
    public sealed class RedisHealthCheck : IHealthCheck
    {
        private readonly IServiceProvider _services;

        public RedisHealthCheck(IServiceProvider services) => _services = services;

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var mux = _services.GetService<IConnectionMultiplexer>();
            if (mux is null)
                return Task.FromResult(HealthCheckResult.Healthy("Redis not configured"));

            return Task.FromResult(mux.IsConnected
                ? HealthCheckResult.Healthy("Redis OK")
                : HealthCheckResult.Degraded("Redis ulanmagan — in-memory fallback ishlayapti"));
        }
    }
}
