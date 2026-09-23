using System.Text;
using System.Text.Json;
using Domain.Interfaces.Telegram;
using Domain.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CommonConfiguration.Telegram
{
    /// <summary>
    /// Telegram Bot API klienti (getUpdates / sendMessage).
    ///
    /// <c>PaymeClient</c> bilan bir xil andoza: timeout options'dan olinadi,
    /// istisno tashlanmaydi — xato logga yoziladi va "muvaffaqiyatsiz" natija qaytadi.
    /// Shu sabab Telegram ishlamay qolsa ham ro'yxatdan o'tish oqimi to'xtamaydi.
    /// </summary>
    public sealed class TelegramBotClient : ITelegramBotClient
    {
        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _http;
        private readonly TelegramOptions _options;
        private readonly ILogger<TelegramBotClient> _logger;

        public TelegramBotClient(HttpClient http, IOptions<TelegramOptions> options, ILogger<TelegramBotClient> logger)
        {
            _options = options.Value;
            _logger = logger;
            _http = http;
            _http.Timeout = TimeSpan.FromSeconds(Math.Max(_options.TimeoutSeconds, 10) + 10);
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.BotToken);

        public Task<bool> SendMessageAsync(long chatId, string text, CancellationToken ct = default)
            => CallAsync("sendMessage", new { chat_id = chatId, text, parse_mode = "HTML" }, ct);

        public Task<bool> RequestContactAsync(long chatId, string text, string buttonText, CancellationToken ct = default)
            => CallAsync("sendMessage", new
            {
                chat_id = chatId,
                text,
                parse_mode = "HTML",
                reply_markup = new
                {
                    keyboard = new[] { new[] { new { text = buttonText, request_contact = true } } },
                    resize_keyboard = true,
                    one_time_keyboard = true
                }
            }, ct);

        public async Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset, int timeoutSeconds, CancellationToken ct = default)
        {
            if (!IsConfigured) return Array.Empty<TelegramUpdate>();

            var body = new { offset, timeout = timeoutSeconds, allowed_updates = new[] { "message" } };

            try
            {
                using var response = await PostAsync("getUpdates", body, ct);
                var json = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[TG] getUpdates {Status}: {Body}", (int)response.StatusCode, Truncate(json));
                    return Array.Empty<TelegramUpdate>();
                }

                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array)
                    return Array.Empty<TelegramUpdate>();

                var updates = new List<TelegramUpdate>();
                foreach (var item in result.EnumerateArray())
                {
                    var parsed = ParseUpdate(item);
                    if (parsed is not null) updates.Add(parsed);
                }

                return updates;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[TG] getUpdates muvaffaqiyatsiz.");
                return Array.Empty<TelegramUpdate>();
            }
        }

        private static TelegramUpdate? ParseUpdate(JsonElement item)
        {
            if (!item.TryGetProperty("update_id", out var idEl)) return null;
            var updateId = idEl.GetInt64();

            if (!item.TryGetProperty("message", out var message))
                return new TelegramUpdate(updateId, null, null, null, null, null);

            long? chatId = message.TryGetProperty("chat", out var chat) && chat.TryGetProperty("id", out var chatIdEl)
                ? chatIdEl.GetInt64()
                : null;

            long? fromId = message.TryGetProperty("from", out var from) && from.TryGetProperty("id", out var fromIdEl)
                ? fromIdEl.GetInt64()
                : null;

            string? text = message.TryGetProperty("text", out var textEl) ? textEl.GetString() : null;

            string? contactPhone = null;
            long? contactUserId = null;
            if (message.TryGetProperty("contact", out var contact))
            {
                contactPhone = contact.TryGetProperty("phone_number", out var phoneEl) ? phoneEl.GetString() : null;
                contactUserId = contact.TryGetProperty("user_id", out var cuEl) ? cuEl.GetInt64() : null;
            }

            return new TelegramUpdate(updateId, chatId, fromId, text, contactPhone, contactUserId);
        }

        private async Task<bool> CallAsync(string method, object body, CancellationToken ct)
        {
            if (!IsConfigured)
            {
                _logger.LogWarning("[TG] {Method}: token sozlanmagan — xabar yuborilmadi.", method);
                return false;
            }

            try
            {
                using var response = await PostAsync(method, body, ct);
                if (response.IsSuccessStatusCode) return true;

                var json = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("[TG] {Method} {Status}: {Body}", method, (int)response.StatusCode, Truncate(json));
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[TG] {Method} muvaffaqiyatsiz.", method);
                return false;
            }
        }

        private Task<HttpResponseMessage> PostAsync(string method, object body, CancellationToken ct)
        {
            var url = $"{_options.BaseUrl.TrimEnd('/')}/bot{_options.BotToken}/{method}";
            var content = new StringContent(JsonSerializer.Serialize(body, JsonOpts), Encoding.UTF8, "application/json");
            return _http.PostAsync(url, content, ct);
        }

        private static string Truncate(string value) => value.Length <= 300 ? value : value[..300];
    }
}
