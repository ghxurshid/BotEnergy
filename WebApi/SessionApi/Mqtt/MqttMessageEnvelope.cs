using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SessionApi.Mqtt
{
    /// <summary>
    /// MQTT xabarlari uchun yagona shablon (device ↔ server, har ikki yo'nalish ham).
    ///
    /// <b>Wire format</b>:
    /// <code>
    /// {
    ///   "envelope": {
    ///     "id": &lt;long&gt;,                  // sender's monotonic counter (request-response paytida echo qilinadi)
    ///     "payload": { /* topic-specific */ }
    ///   },
    ///   "hmac": "&lt;base64 HMAC-SHA256&gt;"   // HMAC over `{id}.{payload_raw_json}`
    /// }
    /// </code>
    ///
    /// <b>HMAC scope</b>: <c>$"{id}.{payload_raw_json}"</c> UTF-8 baytlari.
    /// <c>payload_raw_json</c> — qurilma payload'ni serializatsiya qilgan ekzakt JSON matni.
    /// Server unwrap paytida <see cref="JsonElement.GetRawText"/> orqali aynan shu matnni qaytadan oladi,
    /// shunda qurilma va server canonicalization farqlaridan ozod bo'ladi.
    ///
    /// <b>HMAC key derivation</b>:
    /// <code>hmac_key = SHA-256("BOT-ENERGY-MQTT-HMAC:" + device.SecretKey)</code>
    /// </summary>
    public static class MqttMessageEnvelope
    {
        private const string HmacKeyPrefix = "BOT-ENERGY-MQTT-HMAC:";

        public static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Payload'ni envelope+HMAC bilan o'rab tayyor JSON string qaytaradi. MQTT message body sifatida
        /// to'g'ridan-to'g'ri yuborish mumkin.
        /// </summary>
        public static string Wrap<T>(long id, T payload, string deviceSecretKey)
        {
            var payloadJson = JsonSerializer.Serialize(payload, JsonOpts);
            var hmac = ComputeHmac(id, payloadJson, deviceSecretKey);

            // Outer JSON'ni qo'lda yig'amiz — payloadJson'ni o'zgartirmasdan saqlash uchun
            // (System.Text.Json re-serialize qilishi mumkin va HMAC mos kelmay qoladi).
            var sb = new StringBuilder(payloadJson.Length + 128);
            sb.Append("{\"envelope\":{\"id\":").Append(id)
              .Append(",\"payload\":").Append(payloadJson)
              .Append("},\"hmac\":\"").Append(hmac).Append("\"}");
            return sb.ToString();
        }

        /// <summary>
        /// Qurilmadan kelgan JSON xabarni envelope sifatida ochadi. HMAC tekshiriladi, muvaffaqiyatli bo'lsa
        /// <paramref name="id"/> va <paramref name="payloadJson"/> to'ldiriladi. <paramref name="payloadJson"/>
        /// — qurilma yuborgan original payload JSON matni (handler shu yerdan o'ziga kerakli DTO'ga deserialize qiladi).
        /// </summary>
        public static bool TryUnwrap(
            string envelopeJson,
            string deviceSecretKey,
            out long id,
            out string payloadJson,
            out string error)
        {
            id = 0;
            payloadJson = string.Empty;
            error = string.Empty;

            JsonDocument? doc;
            try
            {
                doc = JsonDocument.Parse(envelopeJson);
            }
            catch (JsonException ex)
            {
                error = $"envelope JSON parse xatosi: {ex.Message}";
                return false;
            }

            using (doc)
            {
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("envelope", out var envelopeElement) ||
                    !root.TryGetProperty("hmac", out var hmacElement))
                {
                    error = "root'da 'envelope' yoki 'hmac' field topilmadi";
                    return false;
                }

                if (envelopeElement.ValueKind != JsonValueKind.Object ||
                    !envelopeElement.TryGetProperty("id", out var idElement) ||
                    !envelopeElement.TryGetProperty("payload", out var payloadElement))
                {
                    error = "envelope ichida 'id' yoki 'payload' field topilmadi";
                    return false;
                }

                if (!idElement.TryGetInt64(out id))
                {
                    error = "envelope.id int64 emas";
                    return false;
                }

                payloadJson = payloadElement.GetRawText();
                var providedHmac = hmacElement.GetString();
                if (string.IsNullOrEmpty(providedHmac))
                {
                    error = "hmac bo'sh";
                    return false;
                }

                byte[] providedBytes;
                try { providedBytes = Convert.FromBase64String(providedHmac); }
                catch (FormatException) { error = "hmac base64 noto'g'ri"; return false; }

                var expectedBytes = ComputeHmacBytes(id, payloadJson, deviceSecretKey);

                if (!CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
                {
                    error = "HMAC mos kelmadi";
                    return false;
                }

                return true;
            }
        }

        private static string ComputeHmac(long id, string payloadJson, string deviceSecretKey)
            => Convert.ToBase64String(ComputeHmacBytes(id, payloadJson, deviceSecretKey));

        private static byte[] ComputeHmacBytes(long id, string payloadJson, string deviceSecretKey)
        {
            var hmacKey = DeriveHmacKey(deviceSecretKey);
            var data = Encoding.UTF8.GetBytes($"{id}.{payloadJson}");
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
