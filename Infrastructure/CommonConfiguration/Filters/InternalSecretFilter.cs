using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CommonConfiguration.Filters
{
    /// <summary>
    /// Servis-servis (yoki infra→servis) chaqiruvlarini <c>X-Internal-Secret</c> header'i
    /// bilan himoyalaydi. JWT o'rniga ishlatiladi, chunki chaqiruvchi foydalanuvchi emas —
    /// masalan EMQX brokerning authn/authz hook'i.
    ///
    /// Ikki qatlam: (1) endpoint umuman public tarmoqda ko'rinmaydi (servis porti firewall
    /// bilan yopiq, gateway bu yo'llarni route qilmaydi), (2) shu filter. Birinchi qatlam
    /// yiqilsa (masalan, noto'g'ri firewall qoidasi) ikkinchisi ushlab qoladi.
    ///
    /// Secret <c>InternalApi:SharedSecret</c> dan olinadi. Production'da u
    /// <c>Env_*</c> placeholder bo'lsa — filter <b>hamma so'rovni rad etadi</b>:
    /// sozlanmagan secret bilan ochiq qolishdan ko'ra yopiq turgani xavfsizroq.
    /// </summary>
    public sealed class InternalSecretFilter : IAsyncActionFilter
    {
        public const string HeaderName = "X-Internal-Secret";

        private readonly byte[]? _expected;
        private readonly ILogger<InternalSecretFilter> _logger;

        public InternalSecretFilter(IConfiguration config, ILogger<InternalSecretFilter> logger)
        {
            _logger = logger;

            // ResolveSecret internal — bir xil assembly ichida (CommonConfiguration) chaqiriladi.
            // "Env_*" placeholder qiymat null sanaladi, ya'ni sozlanmagan deb hisoblanadi.
            var secret = ConfigurationExtensions.ConfigurationAddExtensions
                .ResolveSecret(config, "InternalApi:SharedSecret");

            _expected = string.IsNullOrWhiteSpace(secret) ? null : Encoding.UTF8.GetBytes(secret);
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (_expected is null)
            {
                _logger.LogError(
                    "InternalApi:SharedSecret sozlanmagan — internal endpoint yopiq. " +
                    "Env var bering: InternalApi__SharedSecret.");
                context.Result = new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
                return;
            }

            var provided = context.HttpContext.Request.Headers[HeaderName].FirstOrDefault();
            if (provided is null ||
                !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), _expected))
            {
                _logger.LogWarning(
                    "Internal endpoint'ga noto'g'ri secret bilan murojaat: {Path} ip={Ip}",
                    context.HttpContext.Request.Path,
                    context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "-");
                context.Result = new UnauthorizedResult();
                return;
            }

            await next();
        }
    }
}
