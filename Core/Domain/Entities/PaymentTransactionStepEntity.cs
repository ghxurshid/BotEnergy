using Domain.Entities.BaseEntity;
using Domain.Enums;

namespace Domain.Entities
{
    /// <summary>
    /// To'lov tranzaksiyasining bitta qadami — append-only audit yozuvi.
    /// Hech qachon UPDATE qilinmaydi: har bir o'zgarish yangi step sifatida yoziladi.
    /// </summary>
    public class PaymentTransactionStepEntity : Entity
    {
        public long PaymentTransactionId { get; set; }
        public PaymentTransactionEntity? PaymentTransaction { get; set; }

        public PaymentStepType StepType { get; set; }
        public PaymentStepStatus Status { get; set; } = PaymentStepStatus.Info;

        /// <summary>Provider'ga yuborilgan request payload (JSON). Auth header'larsiz!</summary>
        public string? RequestPayload { get; set; }

        /// <summary>Provider javobi (JSON).</summary>
        public string? ResponsePayload { get; set; }

        public string? Message { get; set; }

        public DateTime OccurredAt { get; set; } = DateTime.Now;
    }
}
