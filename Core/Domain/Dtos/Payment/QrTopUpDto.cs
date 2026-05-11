using Domain.Enums;

namespace Domain.Dtos.Payment
{
    /// <summary>
    /// QR orqali balans to'ldirish so'rovi.
    /// Mobile entry va device MQTT consumer'i ikkalasi ham shu DTO'ni to'ldiradi.
    /// </summary>
    public class QrTopUpRequestDto
    {
        /// <summary>To'lovni boshlagan foydalanuvchi (JWT'dan yoki sessionToken'dan resolve qilingan).</summary>
        public long InitiatedByUserId { get; set; }

        /// <summary>User → o'z balansi (NaturalUser shart). Organization → user'ning org balansi (LegalUser shart).</summary>
        public PaymentPayeeType PayeeType { get; set; } = PaymentPayeeType.User;

        /// <summary>UZS, decimal. Payme bilan o'zaro almashishda tiyin'ga o'tkaziladi (×100).</summary>
        public decimal Amount { get; set; }

        /// <summary>Payme app QR'ida kelgan bir martalik token.</summary>
        public string PaymeToken { get; set; } = string.Empty;

        /// <summary>Device path uchun ixtiyoriy: qaysi sessiya kontekstida.</summary>
        public long? SessionId { get; set; }

        /// <summary>Device path uchun ixtiyoriy: qurilma seriyasi.</summary>
        public string? DeviceSerial { get; set; }

        /// <summary>Mobile entry uchun: Idempotency-Key header'idan keladi.</summary>
        public string? IdempotencyKey { get; set; }
    }

    public class QrTopUpResultDto
    {
        public long TransactionId { get; set; }
        public PaymentStatus Status { get; set; }
        public decimal NewBalance { get; set; }
        public int? ProviderState { get; set; }
        public string ResultMessage { get; set; } = string.Empty;
    }

    public class ReverseTransactionResultDto
    {
        public long TransactionId { get; set; }
        public PaymentStatus Status { get; set; }
        public decimal NewBalance { get; set; }
        public string ResultMessage { get; set; } = string.Empty;
    }
}
