using Domain.Entities.BaseEntity;

namespace Domain.Entities
{
    /// <summary>
    /// Bill acceptor qabul qilgan bitta kupyura — append-only, hech qachon UPDATE qilinmaydi.
    ///
    /// <c>BillSeq</c> — qurilmadagi kupyura ketma-ketligi. <c>(CashSessionId, BillSeq)</c>
    /// unique indeksi takroriy yuborilgan xabar summani ikki marta oshirishiga yo'l qo'ymaydi:
    /// MQTT qayta yuborilishi (device javobni olmay qolsa) normal holat.
    ///
    /// Device/Serial ataylab denormalizatsiya qilingan — audit so'rovlari join'siz filtrlanadi
    /// (<see cref="HoldInvoiceStepEntity"/> bilan bir xil yondashuv).
    /// </summary>
    public class CashSessionBillEntity : Entity
    {
        public long CashSessionId { get; set; }
        public CashSessionEntity? CashSession { get; set; }

        public long DeviceId { get; set; }
        public required string SerialNumber { get; set; }

        /// <summary>Kupyura nominali (UZS).</summary>
        public decimal Denomination { get; set; }

        public string Currency { get; set; } = "UZS";

        /// <summary>Qurilmadagi kupyura tartib raqami — idempotentlik kaliti.</summary>
        public int BillSeq { get; set; }

        public DateTime AcceptedAt { get; set; } = DateTime.Now;
    }
}
