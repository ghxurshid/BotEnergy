using System.Text.Json.Serialization;
using Domain.Enums;

namespace Domain.Dtos.PaymentSession
{
    /// <summary>Mobil ilovadan yangi to'lov niyati (intent) yaratish so'rovi.</summary>
    public class CreatePaymentIntentDto
    {
        public long SessionId { get; set; }
        public long UserId { get; set; }

        /// <summary>Ajratiladigan summa, UZS (so'm). Tiyin'ga server o'giradi.</summary>
        public decimal AmountUzs { get; set; }

        /// <summary>SMS/deep-link yuboriladigan telefon (Invoice usuli uchun; ixtiyoriy).</summary>
        public string? Phone { get; set; }

        /// <summary>
        /// Subscribe usuli: qaysi saqlangan karta bilan to'lansin. Bo'sh bo'lsa asosiy (default) karta.
        /// </summary>
        public long? CardId { get; set; }

        public string? IdempotencyKey { get; set; }
    }

    public class PaymentIntentResultDto
    {
        public long IntentId { get; set; }
        public int SequenceNo { get; set; }
        public PaymentIntentStatus Status { get; set; }

        /// <summary>Qaysi strategiya bajardi.</summary>
        public PaymentMethod Method { get; set; }

        /// <summary>Pul ushlanadimi (Hold) yoki darhol yechiladimi (Charge).</summary>
        public PaymentIntentKind Kind { get; set; }

        public string? ProviderReceiptId { get; set; }
        public long AmountTiyin { get; set; }
        public decimal AmountUzs { get; set; }

        /// <summary>Mijoz ochishi kerak bo'lgan to'lov havolasi (Merchant checkout / Invoice deep-link).</summary>
        public string? CheckoutUrl { get; set; }

        /// <summary>
        /// True — mijoz hali biror amal qilishi kerak (ilovada tasdiqlash / havolani ochish).
        /// False — mablag' allaqachon ta'minlangan (Subscribe: saqlangan karta bilan to'landi).
        /// </summary>
        public bool RequiresUserAction { get; set; }

        public string ResultMessage { get; set; } = string.Empty;
    }

    public class PaymentIntentItemDto
    {
        public long IntentId { get; set; }
        public int SequenceNo { get; set; }
        public PaymentIntentStatus Status { get; set; }
        public PaymentMethod Method { get; set; }
        public PaymentIntentKind Kind { get; set; }
        public long AmountTiyin { get; set; }
        public long ConsumedTiyin { get; set; }
        public string? ProviderReceiptId { get; set; }
        public string? CheckoutUrl { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? FundedAt { get; set; }
        public DateTime? SettledAt { get; set; }
        public string? FailureReason { get; set; }
    }

    public class PaymentSessionDto
    {
        public long PaymentSessionId { get; set; }
        public long SessionId { get; set; }
        public PaymentSessionStatus Status { get; set; }

        /// <summary>Shu sessiya uchun qotirilgan to'lov usuli.</summary>
        public PaymentMethod Method { get; set; }

        /// <summary>
        /// Qurilma egasi bo'lgan merchant. Subscribe usulida mijoz ilovasi shu id bilan
        /// saqlangan kartalarini so'raydi (token merchant kassasiga bog'langan).
        /// </summary>
        public long MerchantId { get; set; }

        public long FundedTiyin { get; set; }
        public long ConsumedTiyin { get; set; }
        public long AvailableTiyin { get; set; }
        public decimal AvailableUzs { get; set; }
        public List<PaymentIntentItemDto> Intents { get; set; } = new();
    }

    /// <summary>
    /// Sessiya balansi o'zgarish eventi — YAGONA model, ikkala kanalga bir xil yuboriladi:
    /// SignalR (SessionBalanceChanged) va MQTT (balance.update).
    /// </summary>
    public class SessionBalanceChangedDto
    {
        public long SessionId { get; set; }
        public long PaymentSessionId { get; set; }
        public long? IntentId { get; set; }
        public PaymentMethod Method { get; set; }
        public long FundedTiyin { get; set; }
        public long ConsumedTiyin { get; set; }
        public long AvailableTiyin { get; set; }
        public decimal AvailableUzs { get; set; }

        /// <summary><see cref="BalanceChangeReasons"/> qiymatlaridan biri — klient shu bo'yicha
        /// UI'ni yangilaydi, so'ng /api/SessionPayment/Balance bilan avtoritar holatni oladi.</summary>
        public string Reason { get; set; } = string.Empty;

        public Guid CorrelationId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;

        // ── Eski sim kontrakti (bir reliz) ──────────────────────────
        // Dalada ishlayotgan qurilma proshivkasi va mobil ilova hali eski nomlarni o'qiydi.
        // Faqat CHIQISHDA dublikat qilinadi; server hech qachon bu maydonlarni o'qimaydi.

        [Obsolete("IntentId ishlating — keyingi relizda olib tashlanadi.")]
        [JsonPropertyName("invoiceId")]
        public long? InvoiceId => IntentId;

        [Obsolete("FundedTiyin ishlating — keyingi relizda olib tashlanadi.")]
        [JsonPropertyName("holdBalanceTiyin")]
        public long HoldBalanceTiyin => FundedTiyin;
    }

    /// <summary>
    /// <see cref="SessionBalanceChangedDto.Reason"/> qiymatlari — SIM KONTRAKTI, qiymatlar
    /// o'zgartirilmaydi. Ba'zilari balansni o'zgartirmaydi (Cancelled/Expired/Failed/InvoiceCreated),
    /// lekin intent HOLATI o'zgargani uchun UI yangilanishi kerak.
    /// </summary>
    public static class BalanceChangeReasons
    {
        /// <summary>Yangi intent yaratildi (mijoz to'lovi kutilmoqda).</summary>
        public const string InvoiceCreated = "InvoiceCreated";

        /// <summary>Hold ushlandi (Subscribe) — pul mijoz kartasida bloklandi.</summary>
        public const string InvoiceHeld = "InvoiceHeld";

        /// <summary>To'lov o'tdi (prepaid: Invoice/Merchant) — pul yechildi.</summary>
        public const string Funded = "Funded";

        public const string Consumed = "Consumed";
        public const string Captured = "Captured";
        public const string Refunded = "Refunded";
        public const string Cancelled = "Cancelled";
        public const string Expired = "Expired";
        public const string Failed = "Failed";
    }
}
