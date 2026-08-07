using System.Diagnostics;
using System.Security.Claims;

namespace Gateway.Middlewares
{
    /// <summary>
    /// Holatni o'zgartiruvchi so'rovlarni audit qiladi: kim, nima, qachon, qanday natija bilan.
    ///
    /// Bu request log EMAS. Request log (Serilog) har bir so'rovni yozadi va 14–30 kun yashaydi;
    /// audit esa faqat POST/PUT/PATCH/DELETE ni yozadi va uzoq saqlanadi. Ikkalasi bir joyda
    /// aralashsa, audit qidiruvi GET shovqinida ko'milib ketadi.
    ///
    /// Body ataylab yozilmaydi: <c>Auth/Login</c> paroli, Payme karta ma'lumoti va shaxsiy
    /// ma'lumotlar log'ga tushmasligi kerak.
    /// </summary>
    public sealed class AuditLoggingMiddleware
    {
        private static readonly HashSet<string> AuditedMethods =
            new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

        private readonly RequestDelegate _next;
        private readonly ILogger<AuditLoggingMiddleware> _logger;

        public AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var shouldAudit = AuditedMethods.Contains(context.Request.Method);
            if (!shouldAudit)
            {
                await _next(context);
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();

                var user = context.User;
                var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
                var group = user?.FindFirst("UserGroup")?.Value ?? "-";

                _logger.LogInformation(
                    "AUDIT {Method} {Path} status={Status} user={UserId} group={UserGroup} " +
                    "ip={ClientIp} ms={ElapsedMs} reqId={RequestId}",
                    context.Request.Method,
                    context.Request.Path.Value,
                    context.Response.StatusCode,
                    userId,
                    group,
                    context.Connection.RemoteIpAddress?.ToString() ?? "-",
                    stopwatch.ElapsedMilliseconds,
                    context.TraceIdentifier);
            }
        }
    }
}
