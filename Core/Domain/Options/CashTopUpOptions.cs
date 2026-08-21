namespace Domain.Options
{
    /// <summary>
    /// Naqd → karta oqimi sozlamalari. Config section: "CashTopUp"
    /// (Infrastructure/CommonConfiguration/ConfigurationFile/Configuration.{env}.json).
    /// </summary>
    public class CashTopUpOptions
    {
        /// <summary>Watcher tick oralig'i (sekund).</summary>
        public int WatcherIntervalSeconds { get; set; } = 10;

        /// <summary>Watcher lease muddati (sekund) — shu vaqt ichida boshqa instansiya olmaydi.</summary>
        public int LeaseSeconds { get; set; } = 60;

        /// <summary>Bir tick'da claim qilinadigan maksimal sessiya soni.</summary>
        public int BatchSize { get; set; } = 20;

        /// <summary>Transport xatolarida maksimal urinishlar — oshsa admin qo'lda hal qiladi.</summary>
        public int MaxAttempts { get; set; } = 8;

        public int BackoffBaseSeconds { get; set; } = 10;
        public int BackoffMaxSeconds { get; set; } = 900;

        /// <summary>
        /// Sessiya shuncha daqiqa harakatsiz tursa yopiladi: summa &gt; 0 bo'lsa avtomatik
        /// commit (pul mijoz kartasiga o'tadi), 0 bo'lsa Expired.
        /// </summary>
        public int IdleTimeoutMinutes { get; set; } = 5;

        /// <summary>Idle tekshiruvi oralig'i (sekund).</summary>
        public int IdleCheckIntervalSeconds { get; set; } = 60;

        /// <summary>
        /// Qabul qilinadigan kupyura nominallari (UZS). Qurilma boshqa qiymat yuborsa rad etiladi —
        /// buzilgan yoki soxta xabar summani sun'iy oshirib yuborishining oldini oladi.
        /// </summary>
        public decimal[] AllowedDenominations { get; set; } =
            { 1000m, 2000m, 5000m, 10000m, 20000m, 50000m, 100000m, 200000m };

        /// <summary>Bitta sessiyada qabul qilinadigan maksimal summa (UZS).</summary>
        public decimal MaxSessionAmount { get; set; } = 50_000_000m;
    }
}
