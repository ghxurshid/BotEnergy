namespace CommonConfiguration.Payments.Payme
{
    /// <summary>
    /// Payme Receipts API uchun konfiguratsiya. Configuration.{env}.json ichidagi "Payme" sektsiyasidan o'qiladi.
    /// Production credentials repository'da saqlanmaydi — server'ga qo'lda joylashtiriladi.
    /// </summary>
    public class PaymeOptions
    {
        public string BaseUrl { get; set; } = "https://checkout.test.paycom.uz/api";
        public string MerchantId { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 30;
    }
}
