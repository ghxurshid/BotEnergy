namespace Domain.Dtos.Session
{
    /// <summary>
    /// Kolonka ekranida ko'rsatiladigan bir martalik QR kod.
    /// Qurilma <see cref="Payload"/> ni QR sifatida chizadi.
    /// </summary>
    public class DeviceQrDto
    {
        /// <summary>Xom kod (faqat server va qurilma uchun).</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// QR ichiga yoziladigan matn: <c>BE1:{code}</c>. Prefiks ilovaga kodni
        /// oddiy seriya-raqamli stikerdan ajratish imkonini beradi.
        /// </summary>
        public string Payload { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }

        /// <summary>Amal qilish muddati (sekund) — qurilma shu oraliqda yangilab turadi.</summary>
        public int TtlSeconds { get; set; }
    }

    /// <summary>Mijoz kolonkadagi QR ni skanerlaganda yuboriladigan so'rov.</summary>
    public class ConnectByQrDto
    {
        public long UserId { get; set; }

        /// <summary>QR ichidan olingan kod (<c>BE1:</c> prefiksi bilan ham, usiz ham qabul qilinadi).</summary>
        public string Code { get; set; } = string.Empty;
    }
}
