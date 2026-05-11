namespace Domain.Interfaces.Payme
{
    /// <summary>
    /// Payme Receipts API'dan qaytadigan receipt'ning sodda ko'rinishi.
    /// To'liq spec'da ko'p maydon bor; biz hozircha balans to'ldirish uchun
    /// kerakli maydonlarni saqlaymiz, qolgani RawResponse'da audit'da yotadi.
    /// </summary>
    public class PaymeReceipt
    {
        public string Id { get; set; } = string.Empty;
        public int State { get; set; }
        public long Amount { get; set; }
        public string? OrderId { get; set; }
    }
}
