using System.Text.Json;
using CommonConfiguration.Attributes;
using Domain.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SessionApi.Controllers
{
    /// <summary>
    /// Payme Merchant API callback'i — Payme BIZGA JSON-RPC qiladi.
    ///
    /// Har bir merchant Payme kabinetida o'z URL'ini ko'rsatadi:
    /// <c>https://&lt;host&gt;/session/api/PaymeMerchant/Callback/{merchantId}</c>
    /// (Gateway orqali; to'g'ridan-to'g'ri SessionApi'da <c>/api/PaymeMerchant/Callback/{merchantId}</c>).
    ///
    /// **Xavfsizlik:** JWT yo'q — Payme <c>Authorization: Basic base64("Paycom:&lt;merchant_key&gt;")</c>
    /// yuboradi va u URL'dagi merchantning kaliti bilan DOIMIY VAQTDA solishtiriladi.
    /// Kalit noto'g'ri bo'lsa -32504 qaytadi (HTTP 200 bilan — Payme shunday kutadi).
    ///
    /// **Javob shakli:** Payme snake_case kalitlarni kutadi, shuning uchun javob global
    /// camelCase siyosatidan chetda, o'z serializer sozlamasi bilan yoziladi.
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [AllowAnonymous]
    public class PaymeMerchantController : ControllerBase
    {
        /// <summary>Payme kalitlarni aynan shu ko'rinishda kutadi — camelCase QILINMAYDI.</summary>
        private static readonly JsonSerializerOptions PaymeJson = new()
        {
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        private readonly IPaymeMerchantGateway _gateway;

        public PaymeMerchantController(IPaymeMerchantGateway gateway)
            => _gateway = gateway;

        /// <summary>
        /// CheckPerformTransaction / CreateTransaction / PerformTransaction /
        /// CancelTransaction / CheckTransaction / GetStatement.
        /// </summary>
        /// <remarks>
        /// Har doim HTTP 200 qaytaradi — nosozlik JSON-RPC <c>error</c> obyektida keladi
        /// (Payme HTTP xatosini qayta-qayta urib, tranzaksiyani osib qo'yadi).
        /// </remarks>
        [HttpPost("{merchantId:long}")]
        [SkipPermissionCheck]
        [Consumes("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Callback(long merchantId, [FromBody] PaymeRpcRequest? request, CancellationToken ct)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Method))
                return PaymeResult(PaymeRpcResponse.Fail(default, PaymeRpcErrors.InvalidRequest()));

            var auth = Request.Headers.TryGetValue("Authorization", out var header)
                ? header.FirstOrDefault()
                : null;

            var response = await _gateway.HandleAsync(merchantId, auth, request, ct);
            return PaymeResult(response);
        }

        private ContentResult PaymeResult(PaymeRpcResponse response)
            => Content(JsonSerializer.Serialize(response, PaymeJson), "application/json");
    }
}
