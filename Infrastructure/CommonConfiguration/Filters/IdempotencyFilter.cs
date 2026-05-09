using System.Security.Claims;
using System.Text.Json;
using CommonConfiguration.Attributes;
using Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace CommonConfiguration.Filters
{
    /// <summary>
    /// [Idempotent] markeri bilan belgilangan action larni "Idempotency-Key" header bo'yicha
    /// cache qiladi. Bir xil key bilan ikkinchi marta yuborilgan request — birinchi javobni qaytaradi.
    /// </summary>
    public sealed class IdempotencyFilter : IAsyncActionFilter
    {
        public const string HeaderName = "Idempotency-Key";

        private static readonly TimeSpan ReservationTtl = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

        private readonly IIdempotencyStore _store;
        private readonly ILogger<IdempotencyFilter> _logger;

        public IdempotencyFilter(IIdempotencyStore store, ILogger<IdempotencyFilter> logger)
        {
            _store = store;
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var marker = context.ActionDescriptor.EndpointMetadata
                .OfType<IdempotentAttribute>()
                .FirstOrDefault();

            if (marker is null)
            {
                await next();
                return;
            }

            if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var headerValues) ||
                string.IsNullOrWhiteSpace(headerValues.FirstOrDefault()))
            {
                if (marker.Required)
                {
                    context.Result = new ObjectResult(new { message = $"{HeaderName} header talab qilinadi." })
                    {
                        StatusCode = StatusCodes.Status400BadRequest
                    };
                    return;
                }

                await next();
                return;
            }

            var headerKey = headerValues.First()!;
            var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anon";
            var route = context.ActionDescriptor.DisplayName ?? "unknown";
            var key = $"{userId}:{route}:{headerKey}";

            var cached = await _store.TryGetAsync(key);
            if (cached is not null)
            {
                _logger.LogInformation("Idempotent replay for {Key}", key);
                context.HttpContext.Response.Headers["Idempotent-Replay"] = "true";
                context.Result = new ContentResult
                {
                    StatusCode = cached.StatusCode,
                    Content = cached.Body,
                    ContentType = "application/json"
                };
                return;
            }

            var reserved = await _store.TryReserveAsync(key, ReservationTtl);
            if (!reserved)
            {
                context.Result = new ObjectResult(new
                {
                    message = "Bir xil Idempotency-Key bilan boshqa so'rov hozir bajarilmoqda. Biroz kuting va qayta urinib ko'ring."
                })
                {
                    StatusCode = StatusCodes.Status409Conflict
                };
                return;
            }

            var executed = await next();

            // 2xx javoblar cache qilinadi; xatolar reservation ozod qilinadi (klient tuzatib retry qila olsin).
            if (executed.Result is ObjectResult obj && obj.StatusCode is >= 200 and < 300)
            {
                var body = JsonSerializer.Serialize(obj.Value);
                await _store.SaveResponseAsync(key, obj.StatusCode ?? 200, body, CacheTtl);
            }
            else
            {
                await _store.ReleaseAsync(key);
            }
        }
    }
}
