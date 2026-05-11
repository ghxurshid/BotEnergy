using Domain.Enums;

namespace Domain.Messaging.Commands
{
    /// <summary>
    /// Server tomonidan qurilmaga yuboriladigan to'lov natijasi.
    /// Oqim: PaymentService → RabbitMQ (PaymentCommandQueue) → DeviceApi DevicePaymentResultConsumer
    ///       → MqttBridge → MQTT (server/{serial}/payment_result) → Qurilma displeyi.
    /// </summary>
    public class DevicePaymentResult
    {
        public string SerialNumber { get; set; } = string.Empty;

        public long TransactionId { get; set; }
        public PaymentStatus Status { get; set; }

        public decimal Amount { get; set; }
        public decimal? NewBalance { get; set; }

        public string? Message { get; set; }

        /// <summary>Qurilma so'rovida bergan client_ref qaytariladi (correlation uchun).</summary>
        public string? ClientRef { get; set; }
    }
}
