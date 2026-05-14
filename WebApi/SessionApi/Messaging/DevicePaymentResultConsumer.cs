using CommonConfiguration.Messaging;
using SessionApi.Mqtt;
using Domain.Messaging;
using Domain.Messaging.Commands;
using Microsoft.Extensions.Logging;

namespace SessionApi.Messaging
{
    /// <summary>
    /// UserApi'dan RabbitMQ orqali kelgan to'lov natijasini MQTT orqali qurilmaga yuboradi.
    /// PaymentCommandQueue: UserApi → RabbitMQ → DeviceApi → MQTT (server/{serial}/payment_result) → Qurilma
    /// </summary>
    public sealed class DevicePaymentResultConsumer : RabbitMqConsumerBase<DevicePaymentResult>
    {
        private readonly MqttBridge _mqttBridge;
        private readonly ILogger<DevicePaymentResultConsumer> _logger;

        public DevicePaymentResultConsumer(
            RabbitMqConnectionManager connectionManager,
            MqttBridge mqttBridge,
            ILogger<DevicePaymentResultConsumer> logger)
            : base(connectionManager, logger, QueueNames.PaymentCommandQueue)
        {
            _mqttBridge = mqttBridge;
            _logger = logger;
        }

        protected override async Task HandleMessageAsync(DevicePaymentResult result)
        {
            _logger.LogInformation(
                "Payment result yuborilmoqda: {Serial} tx={TxId} status={Status}",
                result.SerialNumber, result.TransactionId, result.Status);

            await _mqttBridge.PublishPaymentResultAsync(
                result.SerialNumber,
                result.TransactionId,
                result.Status.ToString().ToLowerInvariant(),
                result.Amount,
                result.NewBalance,
                result.Message,
                result.ClientRef);
        }
    }
}
