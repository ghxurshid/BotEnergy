using Microsoft.AspNetCore.Http;
using System.Net;
using System.Text.Json;

namespace CommonConfiguration.Middlewares
{
    public class BadRequestException : Exception
    {
        public BadRequestException(string message) : base(message) { }
    }

    public class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message) { }
    }

    public class UnauthorizedException : Exception
    {
        public UnauthorizedException(string message) : base(message) { }
    }

    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;

        public ExceptionMiddleware(RequestDelegate next)
        {
            _next = next;
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
            var response = context.Response;

            response.ContentType = "application/json";

            var statusCode = HttpStatusCode.InternalServerError;
            var message = "Internal server error";

            switch (ex)
            {
                case BadRequestException:
                    statusCode = HttpStatusCode.BadRequest;
                    message = ex.Message;
                    break;

                case NotFoundException:
                    statusCode = HttpStatusCode.NotFound;
                    message = ex.Message;
                    break;

                case UnauthorizedException:
                    statusCode = HttpStatusCode.Unauthorized;
                    message = ex.Message;
                    break;

                default:
                    message = ex.Message; // prod’da bu line o‘rniga generic message beriladi
                    break;
            }

            response.StatusCode = (int)statusCode;

            var result = JsonSerializer.Serialize(new
            {
                success = false,
                error = message
            });

            return response.WriteAsync(result);
        }
    }
}
