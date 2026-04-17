using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace CommonConfiguration.Middlewares
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}: {Message}",
                context.Request.Method, context.Request.Path, ex.Message);

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[EXCEPTION] {DateTime.Now:HH:mm:ss} {context.Request.Method} {context.Request.Path}");
            Console.WriteLine($"  Type:    {ex.GetType().FullName}");
            Console.WriteLine($"  Message: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"  Inner:   {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var result = JsonSerializer.Serialize(new
            {
                success = false,
                error = "Kutilmagan xatolik yuz berdi."
            });

            return context.Response.WriteAsync(result);
        }
    }
}