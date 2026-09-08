using Domain.Entities.BaseEntity;
using Domain.Enums;

namespace Domain.Entities
{
    /// <summary>
    /// Payment intent audit trail qadami — append-only, hech qachon UPDATE qilinmaydi.
    /// Merchant/Device/Session/User maydonlari ataylab denormalizatsiya qilingan —
    /// audit so'rovlari join'siz filtrlanadi (spec talabi).
    /// </summary>
    public class PaymentIntentStepEntity : Entity
    {
        public long PaymentIntentId { get; set; }
        public PaymentIntentEntity? PaymentIntent { get; set; }

        // Denormalized audit bog'lamlari
        public long PaymentSessionId { get; set; }
        public long SessionId { get; set; }
        public long MerchantId { get; set; }
        public long DeviceId { get; set; }
        public long UserId { get; set; }

        public PaymentIntentStepType StepType { get; set; }
        public PaymentStepStatus Status { get; set; } = PaymentStepStatus.Info;

        /// <summary>Provider'ga yuborilgan request payload (JSON). Auth header'larsiz!</summary>
        public string? RequestPayload { get; set; }

        /// <summary>Provider javobi (JSON).</summary>
        public string? ResponsePayload { get; set; }

        public string? Message { get; set; }

        public Guid CorrelationId { get; set; }

        public DateTime OccurredAt { get; set; } = DateTime.Now;
    }
}
