using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeviceApi.Mqtt
{
    /// <summary>
    /// MQTT payload xavfsizligi:
    ///  - AES-256-GCM (maxfiylik + tag-based integrity)
    ///  - HMAC-SHA256 qo'shimcha qatlam (alohida kalit bilan)
    ///  - Unix timestamp (replay attack himoyasi)
    /// AES va HMAC kalitlari Device.SecretKey dan ikki xil prefiks bilan SHA-256 orqali derive qilinadi.
    /// Qurilma tomonida ham xuddi shu derivation va format qo'llanilishi shart.
    /// </summary>
    public static class SecureMqttEnvelope
    {
        private const int IvSize = 12;
        private const int TagSize = 16;
        private const int NonceSize = 16;

        private const string AesKeyPrefix = "BOT-ENERGY-MQTT-AES:";
        private const string HmacKeyPrefix = "BOT-ENERGY-MQTT-HMAC:";

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static string Wrap(string plaintextJson, string deviceSecretKey)
        {
            var (aesKey, hmacKey) = DeriveKeys(deviceSecretKey);

            var iv = RandomNumberGenerator.GetBytes(IvSize);
            var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(NonceSize / 2));
            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var plainBytes = Encoding.UTF8.GetBytes(plaintextJson);
            var cipherBytes = new byte[plainBytes.Length];
            var tag = new byte[TagSize];

            using (var aes = new AesGcm(aesKey, TagSize))
            {
                var aad = BuildAad(ts, nonce, iv);
                aes.Encrypt(iv, plainBytes, cipherBytes, tag, aad);
            }

            var ctWithTag = new byte[cipherBytes.Length + tag.Length];
            Buffer.BlockCopy(cipherBytes, 0, ctWithTag, 0, cipherBytes.Length);
            Buffer.BlockCopy(tag, 0, ctWithTag, cipherBytes.Length, tag.Length);

            var ivB64 = Convert.ToBase64String(iv);
            var ctB64 = Convert.ToBase64String(ctWithTag);

            var sigInput = Encoding.UTF8.GetBytes($"{ts}.{nonce}.{ivB64}.{ctB64}");
            using var hmac = new HMACSHA256(hmacKey);
            var sig = Convert.ToBase64String(hmac.ComputeHash(sigInput));

            var envelope = new SecureEnvelope
            {
                V = 1,
                Ts = ts,
                Nonce = nonce,
                Iv = ivB64,
                Ct = ctB64,
                Sig = sig
            };

            return JsonSerializer.Serialize(envelope, JsonOpts);
        }

        public static bool TryUnwrap(
            string envelopeJson,
            string deviceSecretKey,
            int maxClockSkewSeconds,
            out string plaintextJson,
            out string error)
        {
            plaintextJson = string.Empty;
            error = string.Empty;

            SecureEnvelope? env;
            try
            {
                env = JsonSerializer.Deserialize<SecureEnvelope>(envelopeJson, JsonOpts);
            }
            catch (JsonException)
            {
                error = "noto'g'ri envelope JSON";
                return false;
            }

            if (env is null
                || string.IsNullOrEmpty(env.Iv)
                || string.IsNullOrEmpty(env.Ct)
                || string.IsNullOrEmpty(env.Sig)
                || string.IsNullOrEmpty(env.Nonce))
            {
                error = "to'liq bo'lmagan envelope";
                return false;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var skew = now - env.Ts;
            if (Math.Abs(skew) > maxClockSkewSeconds)
            {
                error = $"timestamp eskirgan yoki kelgusidan (skew={skew}s)";
                return false;
            }

            var (aesKey, hmacKey) = DeriveKeys(deviceSecretKey);

            var sigInput = Encoding.UTF8.GetBytes($"{env.Ts}.{env.Nonce}.{env.Iv}.{env.Ct}");
            byte[] expectedSig;
            using (var hmac = new HMACSHA256(hmacKey))
                expectedSig = hmac.ComputeHash(sigInput);

            byte[] providedSig;
            try { providedSig = Convert.FromBase64String(env.Sig); }
            catch (FormatException) { error = "sig base64 noto'g'ri"; return false; }

            if (!CryptographicOperations.FixedTimeEquals(expectedSig, providedSig))
            {
                error = "HMAC mos kelmadi";
                return false;
            }

            byte[] iv, ctWithTag;
            try
            {
                iv = Convert.FromBase64String(env.Iv);
                ctWithTag = Convert.FromBase64String(env.Ct);
            }
            catch (FormatException)
            {
                error = "iv/ct base64 noto'g'ri";
                return false;
            }

            if (iv.Length != IvSize || ctWithTag.Length < TagSize)
            {
                error = "iv/ct uzunligi xato";
                return false;
            }

            var cipherLen = ctWithTag.Length - TagSize;
            var cipherBytes = new byte[cipherLen];
            var tag = new byte[TagSize];
            Buffer.BlockCopy(ctWithTag, 0, cipherBytes, 0, cipherLen);
            Buffer.BlockCopy(ctWithTag, cipherLen, tag, 0, TagSize);

            var plainBytes = new byte[cipherLen];
            try
            {
                using var aes = new AesGcm(aesKey, TagSize);
                var aad = BuildAad(env.Ts, env.Nonce, iv);
                aes.Decrypt(iv, cipherBytes, tag, plainBytes, aad);
            }
            catch (CryptographicException)
            {
                error = "AES-GCM tag tekshiruvi muvaffaqiyatsiz";
                return false;
            }

            plaintextJson = Encoding.UTF8.GetString(plainBytes);
            return true;
        }

        // Portable AAD format (ESP32/embedded clients ham reproduce qila olishi uchun):
        //   [0..8)   ts as int64 BIG-ENDIAN
        //   [8..8+N) nonce UTF-8 bytes (uzunlik nonce string'idan olinadi — odatda 16 hex chars = 16 bytes)
        //   [..end]  iv raw bytes (12 bytes)
        // Hech qanday length prefix yo'q — barcha qatlamlar bir xil format ishlatadi.
        private static byte[] BuildAad(long ts, string nonce, byte[] iv)
        {
            var nonceBytes = Encoding.UTF8.GetBytes(nonce);
            var aad = new byte[8 + nonceBytes.Length + iv.Length];

            BinaryPrimitives.WriteInt64BigEndian(aad.AsSpan(0, 8), ts);
            Buffer.BlockCopy(nonceBytes, 0, aad, 8, nonceBytes.Length);
            Buffer.BlockCopy(iv, 0, aad, 8 + nonceBytes.Length, iv.Length);

            return aad;
        }

        private static (byte[] aesKey, byte[] hmacKey) DeriveKeys(string deviceSecretKey)
        {
            using var sha = SHA256.Create();
            var aesKey = sha.ComputeHash(Encoding.UTF8.GetBytes(AesKeyPrefix + deviceSecretKey));
            var hmacKey = sha.ComputeHash(Encoding.UTF8.GetBytes(HmacKeyPrefix + deviceSecretKey));
            return (aesKey, hmacKey);
        }

        private sealed class SecureEnvelope
        {
            public int V { get; set; }
            public long Ts { get; set; }
            public string? Nonce { get; set; }
            public string? Iv { get; set; }
            public string? Ct { get; set; }
            public string? Sig { get; set; }
        }
    }
}
