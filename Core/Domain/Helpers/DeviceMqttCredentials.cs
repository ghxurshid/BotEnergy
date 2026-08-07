using System.Security.Cryptography;
using System.Text;

namespace Domain.Helpers
{
    /// <summary>
    /// Qurilmaning MQTT broker parolini <see cref="Entities.DeviceEntity.SecretKey"/> dan
    /// bir tomonlama hosil qiladi.
    ///
    /// <b>Nega derivatsiya, nega alohida ustun emas:</b>
    /// Broker (EMQX) authn hook'i parolni ochiq ko'radi. Agar broker paroli sifatida
    /// <c>SecretKey</c> ning o'zi ishlatilsa, envelope HMAC qatlami ma'nosini yo'qotadi —
    /// brokerga kirgan hujumchi to'g'ri imzolangan xabar yasay oladi. Bu yerda broker
    /// faqat <b>derivatsiya natijasini</b> ko'radi va undan <c>SecretKey</c> ni tiklab
    /// bo'lmaydi (HMAC bir tomonlama). Shu bilan ikkita mustaqil sir hosil bo'ladi,
    /// lekin bazaga yangi ustun qo'shilmaydi.
    ///
    /// <b>Provisioning:</b> qurilma firmware'iga ikkalasi ham yoziladi — HMAC uchun
    /// <c>SecretKey</c>, broker CONNECT uchun shu yerdagi parol. Parolni admin
    /// <c>GET /api/Device/MqttCredentials/{id}</c> orqali oladi.
    /// </summary>
    public static class DeviceMqttCredentials
    {
        /// <summary>
        /// Derivatsiya konteksti. O'zgartirilsa BARCHA qurilmalarning MQTT paroli o'zgaradi —
        /// faqat rejalashtirilgan rotatsiya doirasida (v2, v3, ...) o'zgartiring.
        /// </summary>
        private const string DerivationContext = "botenergy-mqtt-auth-v1";

        /// <summary>
        /// Qurilma uchun MQTT parolini hosil qiladi. Natija URL-safe base64 (44 belgi atrofida).
        /// </summary>
        public static string DerivePassword(string secretKey)
        {
            if (string.IsNullOrWhiteSpace(secretKey))
                throw new ArgumentException("SecretKey bo'sh bo'lishi mumkin emas.", nameof(secretKey));

            var hash = HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(secretKey),
                Encoding.UTF8.GetBytes(DerivationContext));

            return Base64UrlEncode(hash);
        }

        /// <summary>
        /// Berilgan parol qurilmaga mos kelishini constant-time tekshiradi.
        /// Oddiy <c>==</c> taqqoslash timing attack'ga ochiq bo'lardi.
        /// </summary>
        public static bool Verify(string secretKey, string? providedPassword)
        {
            if (string.IsNullOrEmpty(providedPassword))
                return false;

            var expected = Encoding.UTF8.GetBytes(DerivePassword(secretKey));
            var provided = Encoding.UTF8.GetBytes(providedPassword);

            return CryptographicOperations.FixedTimeEquals(expected, provided);
        }

        private static string Base64UrlEncode(byte[] bytes)
            => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
