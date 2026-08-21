namespace Domain.Options
{
    /// <summary>
    /// Kartaga pul o'tkazuvchi bank servisi sozlamalari. Config section: "Bank".
    ///
    /// Hozircha faqat <c>Fake</c> provider mavjud. Haqiqiy bank integratsiyasi kelganda
    /// <see cref="Provider"/> qiymati o'zgaradi va yangi <c>ICardPayoutClient</c>
    /// implementatsiyasi registratsiya qilinadi — chaqiruvchi kodga tegilmaydi.
    /// </summary>
    public class BankOptions
    {
        public const string FakeProvider = "Fake";

        public string Provider { get; set; } = FakeProvider;

        public string? BaseUrl { get; set; }

        public int TimeoutSeconds { get; set; } = 30;

        public FakeBankOptions Fake { get; set; } = new();
    }

    /// <summary>
    /// Fake bank xulq-atvori — <c>PayoutFailed</c> va watcher retry oqimini
    /// haqiqiy bank'siz sinash uchun.
    /// </summary>
    public class FakeBankOptions
    {
        /// <summary>Sun'iy kechikish (millisekund) — sekin javobni taqlid qiladi.</summary>
        public int DelayMs { get; set; } = 200;

        /// <summary>
        /// Payout'ning transport xatosi bilan yiqilish ehtimoli (0..1).
        /// 1 — har doim yiqiladi (retry oqimini sinash uchun), 0 — hech qachon.
        /// </summary>
        public double FailureRate { get; set; }

        /// <summary>
        /// Shu prefiks bilan boshlanadigan karta bank tomonidan RAD etiladi
        /// (yakuniy xato — qayta urinilmaydi). Tekshiruv oqimini sinash uchun.
        /// </summary>
        public string RejectCardPrefix { get; set; } = "0000";
    }
}
