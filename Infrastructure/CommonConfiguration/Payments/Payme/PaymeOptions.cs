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

        /// <summary>
        /// <c>cards.create</c> da <c>save</c> qiymati.
        ///
        /// Kartani KEYINCHALIK ishlatish (hold/recurrent) va SMS bilan tasdiqlash uchun
        /// <c>true</c> bo'lishi kerak — lekin buning uchun kassada "kartani saqlash
        /// (Subscribe)" imkoniyati yoqilgan bo'lishi shart. Kassa yoqilmagan bo'lsa
        /// Payme <c>save=true</c> ni rad etadi, <c>save=false</c> da esa token
        /// saqlanmaydi va <c>cards.get_verify_code</c> SMS yubormaydi.
        /// </summary>
        public bool SaveCard { get; set; } = true;
    }
}
