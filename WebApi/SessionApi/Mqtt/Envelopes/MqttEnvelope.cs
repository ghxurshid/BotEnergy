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

    /// <summary>HMAC mos kelmaganda aniqlangan sabab — logdan to'g'ri harakatni tanlash uchun.</summary>
    public enum HmacMismatchCause
    {
        /// <summary>
        /// Kalit eskirgan/noto'g'ri yoki envelope maydonlari (id/type/timestamp) o'zgartirilgan.
        /// Amaliyotda deyarli har doim: qurilmadagi SecretKey DB'dagisiga teng emas
        /// (qurilma qayta ro'yxatdan o'tkazilgan — RegisterAsync yangi kalit generatsiya qiladi).
        /// </summary>
        KeyOrContent = 0,

        /// <summary>
        /// Kalit to'g'ri, lekin jo'natuvchi imzolagan payload MATNI yuborgan matndan farq qiladi
        /// (bo'shliq/pretty-print). HMAC xom matn ustidan hisoblanadi — canonicalization YO'Q.
        /// </summary>
        PayloadTextDiffers = 1
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

        /// <summary>
        /// HMAC nega mos kelmaganini ajratadi — dalada bu ikki sabab butunlay boshqa ishni talab qiladi:
        /// kalit eskirgan bo'lsa qurilmani qayta provisioning qilish kerak, matn farqi bo'lsa
        /// firmware payload'ni imzolagan matndan boshqacha yuboryapti.
        /// </summary>
        public static HmacMismatchCause DiagnoseMismatch(MqttEnvelope envelope, string deviceSecretKey)
        {
            // Payload matnini siqib (bo'shliqlarsiz) qayta tekshiramiz: shunda mos kelsa,
            // kalit TO'G'RI — jo'natuvchi imzolagan matn bilan yuborgan matni farq qilgan
            // (masalan pretty-print yoki qayta serializatsiya oraliqda).
            try
            {
                using var doc = JsonDocument.Parse(envelope.PayloadJson);
                var compact = JsonSerializer.Serialize(doc.RootElement);

                if (!string.Equals(compact, envelope.PayloadJson, StringComparison.Ordinal))
                {
                    var alt = ComputeHmacBytes(envelope.Id, envelope.Type, envelope.Timestamp, compact, deviceSecretKey);
                    if (TryDecode(envelope.Hmac, out var provided)
                        && CryptographicOperations.FixedTimeEquals(alt, provided))
                        return HmacMismatchCause.PayloadTextDiffers;
                }
            }
            catch (JsonException)
            {
                // Payload allaqachon parse bo'lgan — bu yerga tushishi kutilmaydi.
            }

            return HmacMismatchCause.KeyOrContent;
        }

        /// <summary>
        /// Kalitning barmoq izi (SHA-256 ning dastlabki 8 hex belgisi). Kalitning O'ZI logga
        /// hech qachon yozilmaydi; qurilma tomonida ham shu qiymatni hisoblab solishtirish mumkin.
        /// </summary>
        public static string KeyFingerprint(string deviceSecretKey)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(deviceSecretKey));
            return Convert.ToHexString(hash)[..8].ToLowerInvariant();
        }

        /// <summary>Base64 HMAC ning solishtirishga yetarli qisqa ko'rinishi (sir emas — u simda ochiq ketadi).</summary>
        public static string ShortHmac(string base64Hmac)
            => string.IsNullOrEmpty(base64Hmac) ? "(yo'q)"
             : base64Hmac.Length <= 8 ? base64Hmac
             : base64Hmac[..8];

        private static bool TryDecode(string base64, out byte[] bytes)
        {
            try { bytes = Convert.FromBase64String(base64); return true; }
            catch (FormatException) { bytes = Array.Empty<byte>(); return false; }
        }

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
