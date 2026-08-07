using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Gateway.Extensions
{
    /// <summary>
    /// Gateway'ning uch qatlamli rate limiting'i:
    /// <list type="number">
    /// <item><b>Global (IP)</b> — barcha route'lar uchun umumiy himoya to'ri.</item>
    /// <item><b>auth-strict</b> — login/OTP endpointlari, brute-force'ga qarshi qattiq limit.</item>
    /// <item><b>per-user</b> — token bucket: bitta akkaunt burst qila oladi, lekin barqaror
    /// tezligi cheklangan. Token yo'q bo'lsa IP bo'yicha fixed window'ga tushadi.</item>
    /// </list>
    ///
    /// Eslatma: limiter in-memory. Gateway bir nechta replikaga chiqqanda haqiqiy limit
    /// replika soniga ko'payadi — bu bosqichda tolerant, chunki birinchi mudofaa chizig'i
    /// Nginx <c>limit_req</c> (u ham per-IP, lekin bitta edge'da).
    /// </summary>
    public static class GatewayRateLimitingExtensions
    {
        public const string AuthStrictPolicy = "auth-strict";
        public const string PerUserPolicy = "per-user";

        public static IServiceCollection AddGatewayRateLimiting(
            this IServiceCollection services, IConfiguration config)
        {
            var globalPerMinute = config.GetValue("RateLimit:GlobalPerMinute", 300);
            var authPerMinute = config.GetValue("RateLimit:AuthPerMinute", 10);
            var perUserBurst = config.GetValue("RateLimit:PerUserBurst", 120);
            var perUserPerMinute = config.GetValue("RateLimit:PerUserPerMinute", 60);

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = (context, _) =>
                {
                    context.HttpContext.Response.Headers.RetryAfter = "60";
                    return ValueTask.CompletedTask;
                };

                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        ClientKey(httpContext),
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = globalPerMinute,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        }));

                options.AddPolicy(AuthStrictPolicy, httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        ClientKey(httpContext),
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = authPerMinute,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        }));

                options.AddPolicy(PerUserPolicy, httpContext =>
                {
                    var userId = httpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                    if (string.IsNullOrEmpty(userId))
                    {
                        return RateLimitPartition.GetFixedWindowLimiter(
                            ClientKey(httpContext),
                            _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = perUserPerMinute,
                                Window = TimeSpan.FromMinutes(1),
                                QueueLimit = 0
                            });
                    }

                    return RateLimitPartition.GetTokenBucketLimiter(
                        $"user:{userId}",
                        _ => new TokenBucketRateLimiterOptions
                        {
                            TokenLimit = perUserBurst,
                            TokensPerPeriod = perUserPerMinute,
                            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        });
                });
            });

            return services;
        }

        /// <summary>
        /// Partition kaliti — haqiqiy klient IP. UseForwardedHeaders() dan keyin ishlaydi;
        /// usiz barcha so'rovlar Nginx IP'siga tushib, bitta foydalanuvchi hammani bloklaydi.
        /// </summary>
        private static string ClientKey(HttpContext context)
            => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
