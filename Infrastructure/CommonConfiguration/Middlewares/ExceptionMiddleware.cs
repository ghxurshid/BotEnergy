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
            // DB cheklov xatolari (unique/FK/check/not-null) dastur nosozligi emas — kiritilgan
            // ma'lumot muammosi. Ularni 500 o'rniga tushunarli javob bilan qaytaramiz: servis
            // qatlamidagi oldindan tekshiruv poyga holatida (ikki so'rov bir vaqtda) yoki
            // e'tibordan chetda qolgan yangi indeksda o'tkazib yuborishi mumkin. Bu — yakuniy to'r,
            // shu sabab hech qanday cheklov xatosi "Kutilmagan xatolik" bo'lib chiqmaydi.
            if (DbErrorTranslator.Translate(ex) is { } known)
            {
                _logger.LogWarning(ex, "DB cheklov xatosi {Method} {Path} → {Status}",
                    context.Request.Method, context.Request.Path, known.Status);

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = known.Status;

                // "message" — controller'lardagi xato javoblari bilan bir xil shakl.
                return context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    success = false,
                    message = known.Message,
                    error = known.Message
                }));
            }

            // Serilog console+file sink'lariga bitta structured yozuv — Console.WriteLine dublikati olib tashlandi.
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}",
                context.Request.Method, context.Request.Path);

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