using Domain.Enums;

namespace UserApi.Models.Requests
{
    /// <summary>
    /// QR orqali balansni to'ldirish so'rovi.
    /// PayeeType=Organization bo'lsa, foydalanuvchi LegalUser bo'lishi va Permissions.PaymentTopUpOrganization
    /// permissioniga ega bo'lishi kerak (controller [RequirePermission] orqali tekshiriladi).
    /// </summary>
    public class QrTopUpRequest
    {
        public PaymentPayeeType PayeeType { get; set; } = PaymentPayeeType.User;

        /// <summary>UZS, decimal.</summary>
        public decimal Amount { get; set; }

        /// <summary>Payme app QR'ida kelgan bir martalik token.</summary>
        public string PaymeToken { get; set; } = string.Empty;

        /// <summary>Ixtiyoriy: agar to'lov sessiya kontekstida bo'lsa.</summary>
        public long? SessionId { get; set; }
    }
}
