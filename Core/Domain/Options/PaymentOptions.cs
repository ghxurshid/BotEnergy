using Domain.Enums;

namespace Domain.Options
{
    /// <summary>
    /// Sessiya to'lov oqimi sozlamalari — barcha strategiyalar uchun umumiy.
    /// Config section: "Payments"
    /// (Infrastructure/CommonConfiguration/ConfigurationFile/Configuration.{env}.json).
    /// </summary>
    public class PaymentOptions
    {
        /// <summary>Watcher tick oralig'i (sekund).</summary>
        public int WatcherIntervalSeconds { get; set; } = 5;

        /// <summary>WaitingForConfirmation polling oralig'i (sekund).</summary>
        public int PollSeconds { get; set; } = 3;

        /// <summary>Watcher lease muddati (sekund) — shu vaqt ichida boshqa tick olmaydi.</summary>
        public int LeaseSeconds { get; set; } = 30;

        /// <summary>Bir tick'da claim qilinadigan maksimal intent soni.</summary>
        public int BatchSize { get; set; } = 20;

        /// <summary>Transient xatoda maksimal urinishlar — oshsa Failed (operator).</summary>
        public int MaxAttempts { get; set; } = 8;

        public int BackoffBaseSeconds { get; set; } = 5;
        public int BackoffMaxSeconds { get; set; } = 300;

        /// <summary>Mijoz shu vaqt ichida to'lamasa intent Expired qilinadi (daqiqa).</summary>
        public int IntentTtlMinutes { get; set; } = 30;

        /// <summary>Bir sessiyada bir vaqtda mavjud bo'lishi mumkin aktiv intent'lar.</summary>
        public int MaxIntentsPerSession { get; set; } = 10;

        /// <summary>True — chek yaratilgach mijoz telefoniga SMS yuboriladi (Invoice usuli).</summary>
        public bool SendReceiptToPhone { get; set; } = false;

        /// <summary>
        /// Merchant sozlamasi bo'lmaganda ishlatiladigan usul. Merchant o'z sozlamasini
        /// runtime'da qo'yganda bu qiymat ishlatilmaydi.
        /// </summary>
        public PaymentMethod DefaultMethod { get; set; } = PaymentMethod.Subscribe;

        /// <summary>
        /// Sessiya hisob-kitobda (Settling) shuncha vaqt turgach, qolgan yagona to'siq
        /// Failed intent bo'lsa — QURILMA sessiyasi yopiladi (foydalanuvchi yangi sessiya
        /// ocha olsin), moliyaviy yakun esa operator uchun ochiq qoladi.
        /// </summary>
        public int SettlingGraceMinutes { get; set; } = 15;

        /// <summary>Payme checkout havolasining bazasi (Merchant API).</summary>
        public string CheckoutBaseUrl { get; set; } = "https://checkout.paycom.uz";
    }
}
