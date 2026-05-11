namespace Domain.Messaging.Events
{
    /// <summary>
    /// Qurilmadan kelgan QR to'lov so'rovi.
    /// Oqim: Qurilma → MQTT (device/{serial}/payment_qr) → DeviceApi MqttBridge → RabbitMQ
    ///       PaymentEventQueue → UserApi DevicePaymentEventConsumer → IPaymentService.
    /// </summary>
    public class DevicePaymentRequest
    {
        public string SerialNumber { get; set; } = string.Empty;

        /// <summary>Mobile sessiya tokeni — userId'ni resolve qilish uchun.</summary>
        public string SessionToken { get; set; } = string.Empty;

        /// <summary>UZS, decimal. User device klaviaturasida kiritgan summa.</summary>
        public decimal Amount { get; set; }

        /// <summary>Payme app QR'idan o'qilgan bir martalik token.</summary>
        public string PaymeToken { get; set; } = string.Empty;

        /// <summary>
        /// Ixtiyoriy: qurilma tomonidan generatsiya qilingan idempotency identifikator
        /// (bir xil QR ikki marta o'qilsa, server xuddi shu transaksiyani qaytaradi).
        /// </summary>
        public string? ClientRef { get; set; }
    }
}
