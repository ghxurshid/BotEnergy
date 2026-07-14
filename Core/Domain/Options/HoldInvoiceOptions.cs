namespace Domain.Options
{
    /// <summary>
    /// Hold invoice oqimi sozlamalari. Config section: "HoldInvoices"
    /// (Infrastructure/CommonConfiguration/ConfigurationFile/Configuration.{env}.json).
    /// </summary>
    public class HoldInvoiceOptions
    {
        /// <summary>Watcher tick oralig'i (sekund).</summary>
        public int WatcherIntervalSeconds { get; set; } = 5;

        /// <summary>WaitingForConfirmation polling oralig'i (sekund).</summary>
        public int PollSeconds { get; set; } = 3;

        /// <summary>Watcher lease muddati (sekund) — shu vaqt ichida boshqa tick olmaydi.</summary>
        public int LeaseSeconds { get; set; } = 30;

        /// <summary>Bir tick'da claim qilinadigan maksimal invoice soni.</summary>
        public int BatchSize { get; set; } = 20;

        /// <summary>Transient xatoda maksimal urinishlar — oshsa Failed (operator).</summary>
        public int MaxAttempts { get; set; } = 8;

        public int BackoffBaseSeconds { get; set; } = 5;
        public int BackoffMaxSeconds { get; set; } = 300;

        /// <summary>Mijoz shu vaqt ichida to'lamasa invoice Expired qilinadi (daqiqa).</summary>
        public int InvoiceTtlMinutes { get; set; } = 30;

        /// <summary>Bir sessiyada bir vaqtda mavjud bo'lishi mumkin aktiv invoice'lar.</summary>
        public int MaxInvoicesPerSession { get; set; } = 10;

        /// <summary>True — receipt yaratilgach mijoz telefoniga SMS invoice yuboriladi.</summary>
        public bool SendReceiptToPhone { get; set; } = false;
    }
}
