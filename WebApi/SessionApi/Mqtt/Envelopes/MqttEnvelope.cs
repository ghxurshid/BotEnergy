using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SessionApi.Mqtt.Abstractions
{
    /// <summary>
    /// Yagona MQTT message envelope (device ↔ server, har ikki yo'nalish).
    ///
    /// <b>Wire format</b>:
    /// <code>
    /// {
    ///   "id": &lt;long&gt;,         // sender's monotonic counter
    ///   "type": "&lt;string&gt;",    // action nomi — "session.connect", "process.start", ...
    ///   "timestamp": &lt;long&gt;,  // unix seconds (UTC) — yaratilgan vaqt
    ///   "payload": { ... },     // topic/type-specific
    ///   "hmac": "&lt;base64&gt;"    // HMAC-SHA256
    /// }
    /// </code>
    ///
    /// <b>HMAC scope</b>: <c>$"{id}.{type}.{timestamp}.{payload_raw_json}"</c> UTF-8.
    /// <c>payload_raw_json</c> — sender'ning ekzakt JSON matni (canonicalization yo'q).
    ///
    /// <b>HMAC key</b>: <c>SHA-256("BOT-ENERGY-MQTT-HMAC:" + device.SecretKey)</c>.
    /// </summary>
    public sealed class MqttEnvelope
    {
        public long Id { get; init; }
        public string Type { get; init; } = string.Empty;
        public long Timestamp { get; init; }
        public string PayloadJson { get; init; } = "{}";
        public string Hmac { get; init; } = string.Empty;
    }

    public static class MqttEnvelopeSerializer
    {
        private const string HmacKeyPrefix = "BOT-ENERGY-MQTT-HMAC:";

        public static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Payload'ni envelope+HMAC bilan o'rab tayyor JSON string qaytaradi.
        /// </summary>
        public static string Wrap<T>(long id, string type, long timestamp, T payload, string deviceSecretKey)
        {
            var payloadJson = JsonSerializer.Serialize(payload, JsonOpts);
            var hmac = ComputeHmac(id, type, timestamp, payloadJson, deviceSecretKey);

            var sb = new StringBuilder(payloadJson.Length + 256);
            sb.Append("{\"id\":").Append(id)
              .Append(",\"type\":\"").Append(JsonEncodedText.Encode(type)).Append('"')
              .Append(",\"timestamp\":").Append(timestamp)
              .Append(",\"payload\":").Append(payloadJson)
              .Append(",\"hmac\":\"").Append(hmac).Append("\"}");
            return sb.ToString();
        }

        /// <summary>
        /// Raw JSON ni envelope sifatida parse qiladi. HMAC tekshirilmaydi — bu keyingi
        /// middleware (<c>HmacValidationMiddleware</c>) ning vazifasi.
        /// </summary>
        public static bool TryParse(string rawJson, out MqttEnvelope envelope, out string error)
        {
            envelope = new MqttEnvelope();
            error = string.Empty;

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(rawJson);
            }
            catch (JsonException ex)
            {
                error = $"envelope JSON parse xatosi: {ex.Message}";
                return false;
            }

            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    error = "envelope root JSON object emas";
                    return false;
                }

                if (!root.TryGetProperty("id", out var idEl) || !idEl.TryGetInt64(out var id))
                {
                    error = "envelope.id (int64) yo'q";
                    return false;
                }

                if (!root.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
                {
                    error = "envelope.type (string) yo'q";
                    return false;
                }

                if (!root.TryGetProperty("timestamp", out var tsEl) || !tsEl.TryGetInt64(out var ts))
                {
                    error = "envelope.timestamp (int64 unix seconds) yo'q";
                    return false;
                }

                if (!root.TryGetProperty("payload", out var payloadEl))
                {
                    error = "envelope.payload yo'q";
                    return false;
                }

                if (!root.TryGetProperty("hmac", out var hmacEl) || hmacEl.ValueKind != JsonValueKind.String)
                {
                    error = "envelope.hmac (string) yo'q";
                    return false;
                }

                envelope = new MqttEnvelope
                {
                    Id = id,
                    Type = typeEl.GetString() ?? string.Empty,
                    Timestamp = ts,
                    PayloadJson = payloadEl.GetRawText(),
                    Hmac = hmacEl.GetString() ?? string.Empty
                };
                return true;
            }
        }

        /// <summary>
        /// Envelope HMAC ni device secret bilan tekshiradi (constant-time).
        /// </summary>
        public static bool VerifyHmac(MqttEnvelope envelope, string deviceSecretKey)
        {
            if (string.IsNullOrEmpty(envelope.Hmac)) return false;

            byte[] provided;
            try { provided = Convert.FromBase64String(envelope.Hmac); }
            catch (FormatException) { return false; }

            var expected = ComputeHmacBytes(envelope.Id, envelope.Type, envelope.Timestamp, envelope.PayloadJson, deviceSecretKey);
            return CryptographicOperations.FixedTimeEquals(expected, provided);
        }

        public static string ComputeHmac(long id, string type, long timestamp, string payloadJson, string deviceSecretKey)
            => Convert.ToBase64String(ComputeHmacBytes(id, type, timestamp, payloadJson, deviceSecretKey));

        private static byte[] ComputeHmacBytes(long id, string type, long timestamp, string payloadJson, string deviceSecretKey)
        {
            var hmacKey = DeriveHmacKey(deviceSecretKey);
            var data = Encoding.UTF8.GetBytes($"{id}.{type}.{timestamp}.{payloadJson}");
            using var hmac = new HMACSHA256(hmacKey);
            return hmac.ComputeHash(data);
        }

        private static byte[] DeriveHmacKey(string deviceSecretKey)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes(HmacKeyPrefix + deviceSecretKey));
        }
    }
}
