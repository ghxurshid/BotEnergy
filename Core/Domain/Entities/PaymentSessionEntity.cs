using Domain.Entities.BaseEntity;
using Domain.Enums;

namespace Domain.Entities
{
    /// <summary>
    /// Device sessiyasiga bog'langan to'lov konteksti. Sessiya ochilganda balance=0 bilan yaratiladi,
    /// hold invoice'lar Hold holatiga o'tganda balans oshadi, dispense FIFO tartibda yeydi.
    /// Sessiya yopilganda Settling → barcha invoice'lar terminal bo'lgach Settled.
    /// Barcha summalar integer TIYIN (1 so'm = 100 tiyin).
    /// </summary>
    public class PaymentSessionEntity : Entity
    {
        public long SessionId { get; set; }
        public SessionEntity? Session { get; set; }

        public long UserId { get; set; }
        public long DeviceId { get; set; }

        /// <summary>Device egasi — invoice'lar shu merchant Payme credential'lari bilan yaratiladi.</summary>
        public long MerchantId { get; set; }
        public MerchantEntity? Merchant { get; set; }

        public PaymentSessionStatus Status { get; set; } = PaymentSessionStatus.Active;

        /// <summary>Hold holatiga o'tgan invoice'lar summasi (tiyin).</summary>
        public long HoldBalanceTiyin { get; set; }

        /// <summary>Dispense'larga ishlatilgan summa (tiyin). Available = HoldBalance - Consumed.</summary>
        public long ConsumedTiyin { get; set; }

        /// <summary>Audit trail'ni bir sessiya bo'ylab bog'lash uchun.</summary>
        public Guid CorrelationId { get; set; } = Guid.NewGuid();

        public DateTime? SettledAt { get; set; }

        /// <summary>Optimistic concurrency (PostgreSQL xmin).</summary>
        public uint RowVersion { get; set; }

        public ICollection<HoldInvoiceEntity>? Invoices { get; set; }
    }
}
