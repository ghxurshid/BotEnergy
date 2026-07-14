using Domain.Enums;

namespace Domain.Dtos.PaymentSession
{
    /// <summary>Mobil ilovadan hold invoice yaratish so'rovi.</summary>
    public class CreateHoldInvoiceDto
    {
        public long SessionId { get; set; }
        public long UserId { get; set; }

        /// <summary>Bloklanadigan summa, UZS (so'm). Tiyin'ga server o'giradi.</summary>
        public decimal AmountUzs { get; set; }

        /// <summary>SMS invoice yuboriladigan telefon (ixtiyoriy; SendReceiptToPhone yoqilgan bo'lsa).</summary>
        public string? Phone { get; set; }

        public string? IdempotencyKey { get; set; }
    }

    public class HoldInvoiceResultDto
    {
        public long InvoiceId { get; set; }
        public int SequenceNo { get; set; }
        public HoldInvoiceStatus Status { get; set; }
        public string? ProviderReceiptId { get; set; }
        public long AmountTiyin { get; set; }
        public decimal AmountUzs { get; set; }
        public string ResultMessage { get; set; } = string.Empty;
    }

    public class HoldInvoiceItemDto
    {
        public long InvoiceId { get; set; }
        public int SequenceNo { get; set; }
        public HoldInvoiceStatus Status { get; set; }
        public long AmountTiyin { get; set; }
        public long ConsumedTiyin { get; set; }
        public string? ProviderReceiptId { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? HoldAt { get; set; }
        public DateTime? SettledAt { get; set; }
        public string? FailureReason { get; set; }
    }

    public class PaymentSessionDto
    {
        public long PaymentSessionId { get; set; }
        public long SessionId { get; set; }
        public PaymentSessionStatus Status { get; set; }
        public long HoldBalanceTiyin { get; set; }
        public long ConsumedTiyin { get; set; }
        public long AvailableTiyin { get; set; }
        public decimal AvailableUzs { get; set; }
        public List<HoldInvoiceItemDto> Invoices { get; set; } = new();
    }

    /// <summary>
    /// Sessiya balansi o'zgarish eventi — YAGONA model, ikkala kanalga bir xil yuboriladi:
    /// SignalR (SessionBalanceChanged) va MQTT (balance.update).
    /// </summary>
    public class SessionBalanceChangedDto
    {
        public long SessionId { get; set; }
        public long PaymentSessionId { get; set; }
        public long? InvoiceId { get; set; }
        public long HoldBalanceTiyin { get; set; }
        public long ConsumedTiyin { get; set; }
        public long AvailableTiyin { get; set; }
        public decimal AvailableUzs { get; set; }

        /// <summary>InvoiceHeld | Consumed | Captured | Refunded | Cancelled | Expired</summary>
        public string Reason { get; set; } = string.Empty;

        public Guid CorrelationId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>SessionBalanceChangedDto.Reason qiymatlari.</summary>
    public static class BalanceChangeReasons
    {
        public const string InvoiceHeld = "InvoiceHeld";
        public const string Consumed = "Consumed";
        public const string Captured = "Captured";
        public const string Refunded = "Refunded";
        public const string Cancelled = "Cancelled";
        public const string Expired = "Expired";
    }
}
