using Domain.Entities.BaseEntity;
using Domain.Enums;

namespace Domain.Entities
{
    /// <summary>
    /// Tashqi to'lov tizimi (hozircha Payme) orqali balans to'ldirish operatsiyasi.
    /// Har bir step <see cref="PaymentTransactionStepEntity"/> sifatida alohida saqlanadi (audit trail).
    /// </summary>
    public class PaymentTransactionEntity : Entity
    {
        public PaymentPayeeType PayeeType { get; set; }

        /// <summary>Mablag' qaytariladigan oddiy foydalanuvchi (PayeeType=User bo'lganda).</summary>
        public long? UserId { get; set; }
        public CustomerUserEntity? User { get; set; }

        /// <summary>Mablag' qaytariladigan tashkilot (PayeeType=Organization bo'lganda).</summary>
        public long? OrganizationId { get; set; }
        public OrganizationEntity? Organization { get; set; }

        /// <summary>To'lovni boshlagan foydalanuvchi (org case'da: org owner).</summary>
        public long InitiatedByUserId { get; set; }

        /// <summary>UZS, decimal. Payme bilan o'zaro almashishda tiyin'ga o'tkaziladi.</summary>
        public decimal Amount { get; set; }

        public string Currency { get; set; } = "UZS";

        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        public PaymentProvider Provider { get; set; } = PaymentProvider.Payme;

        /// <summary>Provider-side receipt identifier (Payme uchun receipt _id).</summary>
        public string? ProviderReceiptId { get; set; }

        /// <summary>Bizning tomondan generatsiya qilingan order_id, providerga yuboriladi. Unique.</summary>
        public string ProviderOrderId { get; set; } = string.Empty;

        /// <summary>Provider-side state (Payme: 4 = paid).</summary>
        public int? ProviderState { get; set; }

        /// <summary>Device entry-pointidan kelgan to'lov bo'lsa — qurilma seriyasi.</summary>
        public string? DeviceSerial { get; set; }

        public long? SessionId { get; set; }
        public SessionEntity? Session { get; set; }

        /// <summary>Idempotent filter bilan integratsiya — duplicate so'rovlar uchun.</summary>
        public string? IdempotencyKey { get; set; }

        public string? FailureReason { get; set; }

        public DateTime? CompletedAt { get; set; }

        public ICollection<PaymentTransactionStepEntity>? Steps { get; set; }
    }
}
