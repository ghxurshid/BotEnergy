using Domain.Interfaces.Payme;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CommonConfiguration.Payments.Payme
{
    /// <summary>
    /// Payme Receipts API uchun typed HttpClient implementation.
    /// JSON-RPC envelope, X-Auth header, va sodda response parsing.
    /// Tarmoq xatolari ham PaymeApiCall.Failure sifatida qaytariladi (throw qilmaydi).
    /// </summary>
    public class PaymeClient : IPaymeClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = null
        };

        private readonly HttpClient _http;
        private readonly PaymeOptions _options;
        private readonly ILogger<PaymeClient> _logger;

        public PaymeClient(HttpClient http, IOptions<PaymeOptions> options, ILogger<PaymeClient> logger)
        {
            _options = options.Value;
            _logger = logger;

            _http = http;
            _http.BaseAddress ??= new Uri(_options.BaseUrl, UriKind.Absolute);
            _http.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        }

        public Task<PaymeApiCall<PaymeReceipt>> CreateReceiptAsync(long amountTiyin, string orderId, bool hold = false, string? description = null, PaymeCredentials? creds = null, CancellationToken ct = default)
            => InvokeAsync("receipts.create", hold
                ? new
                {
                    amount = amountTiyin,
                    account = new { order_id = orderId },
                    hold = true,
                    description
                }
                : (object)new
                {
                    amount = amountTiyin,
                    account = new { order_id = orderId },
                    description
                }, creds, ct);

        public Task<PaymeApiCall<PaymeReceipt>> PayReceiptAsync(string receiptId, string token, PaymeCredentials? creds = null, CancellationToken ct = default)
            => InvokeAsync("receipts.pay", new
            {
                id = receiptId,
                token
            }, creds, ct);

        public Task<PaymeApiCall<PaymeReceipt>> GetReceiptAsync(string receiptId, PaymeCredentials? creds = null, CancellationToken ct = default)
            => InvokeAsync("receipts.get", new
            {
                id = receiptId
            }, creds, ct);

        public Task<PaymeApiCall<PaymeReceipt>> SendReceiptAsync(string receiptId, string phone, PaymeCredentials? creds = null, CancellationToken ct = default)
            => InvokeAsync("receipts.send", new
            {
                id = receiptId,
                phone
            }, creds, ct);

        public Task<PaymeApiCall<PaymeReceipt>> CheckReceiptAsync(string receiptId, PaymeCredentials? creds = null, CancellationToken ct = default)
            => InvokeAsync("receipts.check", new
            {
                id = receiptId
            }, creds, ct);

        public Task<PaymeApiCall<PaymeReceipt>> ConfirmHoldAsync(string receiptId, long? amountTiyin, PaymeCredentials? creds = null, CancellationToken ct = default)
            => InvokeAsync("receipts.confirm_hold", amountTiyin.HasValue
                ? new
                {
                    id = receiptId,
                    amount = amountTiyin.Value
                }
                : (object)new
                {
                    id = receiptId
                }, creds, ct);

        public Task<PaymeApiCall<PaymeReceipt>> CancelReceiptAsync(string receiptId, PaymeCredentials? creds = null, CancellationToken ct = default)
            => InvokeAsync("receipts.cancel", new
            {
                id = receiptId
            }, creds, ct);

        private async Task<PaymeApiCall<PaymeReceipt>> InvokeAsync(string method, object @params, PaymeCredentials? creds, CancellationToken ct)
        {
            var envelope = new
            {
                id = Random.Shared.NextInt64(1, long.MaxValue),
                method,
                @params
            };

            string requestBody;
            try
            {
                requestBody = JsonSerializer.Serialize(envelope, JsonOptions);
            }
            catch (Exception ex)
            {
                return PaymeApiCall<PaymeReceipt>.Failure(
                    PaymeFailureKind.Deserialization,
                    $"Request serialization failed: {ex.Message}",
                    requestBody: string.Empty,
                    responseBody: string.Empty);
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "")
            {
                Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
            };
            var cashboxId = creds?.CashboxId ?? _options.MerchantId;
            var key = creds?.Key ?? _options.Key;
            request.Headers.TryAddWithoutValidation("X-Auth", $"{cashboxId}:{key}");
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };

            HttpResponseMessage response;
            string responseBody;
            try
            {
                response = await _http.SendAsync(request, ct);
                responseBody = await response.Content.ReadAsStringAsync(ct);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Payme request timed out: method={Method}", method);
                return PaymeApiCall<PaymeReceipt>.Failure(
                    PaymeFailureKind.Timeout,
                    "Payme so'rovi vaqt chegarasidan oshib ketdi.",
                    requestBody, string.Empty);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Payme network error: method={Method}", method);
                return PaymeApiCall<PaymeReceipt>.Failure(
                    PaymeFailureKind.Network,
                    $"Tarmoq xatosi: {ex.Message}",
                    requestBody, string.Empty);
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Payme HTTP non-success: method={Method} status={Status}", method, (int)response.StatusCode);
                return PaymeApiCall<PaymeReceipt>.Failure(
                    PaymeFailureKind.HttpError,
                    $"HTTP {(int)response.StatusCode}",
                    requestBody, responseBody);
            }

            return ParseResponse(requestBody, responseBody);
        }

        private static PaymeApiCall<PaymeReceipt> ParseResponse(string requestBody, string responseBody)
        {
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(responseBody);
            }
            catch (JsonException ex)
            {
                return PaymeApiCall<PaymeReceipt>.Failure(
                    PaymeFailureKind.Deserialization,
                    $"Javobni JSON sifatida o'qib bo'lmadi: {ex.Message}",
                    requestBody, responseBody);
            }

            using (doc)
            {
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var errorElem) && errorElem.ValueKind == JsonValueKind.Object)
                {
                    var error = new PaymeError
                    {
                        Code = errorElem.TryGetProperty("code", out var c) && c.TryGetInt32(out var ci) ? ci : 0,
                        Message = errorElem.TryGetProperty("message", out var m) ? m.GetString() ?? string.Empty : string.Empty,
                        Data = errorElem.TryGetProperty("data", out var d) ? d.ToString() : null
                    };
                    return PaymeApiCall<PaymeReceipt>.FromPaymeError(error, requestBody, responseBody);
                }

                if (!root.TryGetProperty("result", out var resultElem))
                {
                    return PaymeApiCall<PaymeReceipt>.Failure(
                        PaymeFailureKind.Deserialization,
                        "Javobda 'result' yoki 'error' maydoni topilmadi.",
                        requestBody, responseBody);
                }

                var receiptElem = resultElem.TryGetProperty("receipt", out var r) ? r : resultElem;

                var receipt = new PaymeReceipt
                {
                    Id = receiptElem.TryGetProperty("_id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
                    State = receiptElem.TryGetProperty("state", out var st) && st.TryGetInt32(out var sti) ? sti : 0,
                    Amount = receiptElem.TryGetProperty("amount", out var am) && am.TryGetInt64(out var ami) ? ami : 0,
                    OrderId = ExtractOrderId(receiptElem)
                };

                return PaymeApiCall<PaymeReceipt>.Success(receipt, requestBody, responseBody);
            }
        }

        private static string? ExtractOrderId(JsonElement receiptElem)
        {
            if (!receiptElem.TryGetProperty("account", out var account))
                return null;

            // account ko'pincha array (Payme spec): [{ "name": "order_id", "value": "..." }]
            if (account.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in account.EnumerateArray())
                {
                    if (entry.TryGetProperty("name", out var n) &&
                        n.GetString() == "order_id" &&
                        entry.TryGetProperty("value", out var v))
                    {
                        return v.GetString();
                    }
                }
                return null;
            }

            // ba'zan obyekt: { "order_id": "..." }
            if (account.ValueKind == JsonValueKind.Object &&
                account.TryGetProperty("order_id", out var oid))
            {
                return oid.GetString();
            }

            return null;
        }
    }
}
