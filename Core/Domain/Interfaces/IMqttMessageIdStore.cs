namespace Domain.Interfaces
{
    /// <summary>
    /// Qurilma ↔ server MQTT xabarlari uchun monotonic id tracker.
    /// Replay protection: qurilma har xabarda o'zining counterini oshiradi, server
    /// <see cref="TryAcceptInboundIdAsync"/> orqali tekshiradi — id avvalgi qabul qilingandan kichik yoki teng bo'lsa rad etiladi.
    /// Server o'zining unsolicited xabarlari uchun <see cref="NextOutboundIdAsync"/> dan keyingi counter qiymatini oladi.
    /// </summary>
    public interface IMqttMessageIdStore
    {
        /// <summary>
        /// Qurilmadan kelgan xabarning id sini tekshiradi. Agar id avvalgi qabul qilingandan katta bo'lsa
        /// — yangi qiymat sifatida saqlanadi va <c>true</c> qaytadi. Aks holda <c>false</c> (replay yoki out-of-order).
        /// </summary>
        Task<bool> TryAcceptInboundIdAsync(string serialNumber, long id);

        /// <summary>
        /// Serverdan qurilmaga unsolicited xabar yuborilayotganda keyingi counter qiymatini qaytaradi
        /// (avvalgisidan +1). Response holatida bu chaqirilmaydi — request id echo qilinadi.
        /// </summary>
        Task<long> NextOutboundIdAsync(string serialNumber);

        /// <summary>
        /// Berilgan qurilma uchun inbound va outbound counter'larni 0'ga tushiradi.
        /// Qurilma muvaffaqiyatli <c>connect</c> yuborganda chaqiriladi — yangi sessiya boshlanishi
        /// qurilmaning reset/flash holatidan keyin counter'lar mos kelishini ta'minlaydi.
        /// HMAC validatsiyasidan keyin va connect handler success natijasidan keyin chaqirilishi shart
        /// (replay zaifligini oldini olish uchun).
        /// </summary>
        Task ResetAsync(string serialNumber);
    }
}
