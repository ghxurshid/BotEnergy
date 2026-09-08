using Domain.Entities.BaseEntity;
using Domain.Enums;

namespace Domain.Entities
{
    /// <summary>
    /// Device sessiyasiga bog'langan to'lov konteksti. Sessiya ochilganda balance=0 bilan yaratiladi,
    /// intent'lar Funded holatiga o'tganda balans oshadi, dispense FIFO tartibda yeydi.
    /// Sessiya yopilganda Settling → barcha intent'lar terminal bo'lgach Settled.
    /// Barcha summalar integer TIYIN (1 so'm = 100 tiyin).
    /// </summary>
    public class PaymentSessionEntity : Entity
    {
        public long SessionId { get; set; }
        public SessionEntity? Session { get; set; }

        public long UserId { get; set; }
        public long DeviceId { get; set; }

        /// <summary>Device egasi — intent'lar shu merchant provider credential'lari bilan yaratiladi.</summary>
        public long MerchantId { get; set; }
        public MerchantEntity? Merchant { get; set; }

        /// <summary>
        /// Shu sessiya uchun QOTIRILGAN to'lov usuli — yaratilishda merchant sozlamasidan olinadi
        /// va sessiya davomida o'zgarmaydi. Merchant sozlamasini runtime'da almashtirsa,
        /// bu sessiya eski usul bilan yakunlanadi (FIFO consume va settlement bir xil
        /// semantikada tugashi uchun).
        /// </summary>
        public PaymentMethod Method { get; set; } = PaymentMethod.Subscribe;

        public PaymentSessionStatus Status { get; set; } = PaymentSessionStatus.Active;

        /// <summary>Funded holatiga o'tgan intent'lar summasi (tiyin).</summary>
        public long FundedTiyin { get; set; }

        /// <summary>Dispense'larga ishlatilgan summa (tiyin). Available = FundedTiyin - Consumed.</summary>
        public long ConsumedTiyin { get; set; }

        /// <summary>Audit trail'ni bir sessiya bo'ylab bog'lash uchun.</summary>
        public Guid CorrelationId { get; set; } = Guid.NewGuid();

        public DateTime? SettledAt { get; set; }

        /// <summary>Optimistic concurrency (PostgreSQL xmin).</summary>
        public uint RowVersion { get; set; }

        public ICollection<PaymentIntentEntity>? Intents { get; set; }
    }
}
