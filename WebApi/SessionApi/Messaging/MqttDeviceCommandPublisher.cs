using Domain.Interfaces;
using SessionApi.Mqtt.Abstractions;
using SessionApi.Mqtt.Handlers;

namespace SessionApi.Messaging
{
    /// <summary>
    /// <see cref="IDeviceCommandPublisher"/> ning to'g'ridan-to'g'ri MQTT impl'i.
    /// Eski RabbitMQ → DeviceCommandConsumer → MQTT zanjirini almashtiradi
    /// (publisher va consumer bir process'da edi, oraliq queue qiymat bermasdi).
    /// </summary>
    public sealed class MqttDeviceCommandPublisher : IDeviceCommandPublisher
    {
        private readonly IMqttPublisher _publisher;

        public MqttDeviceCommandPublisher(IMqttPublisher publisher) => _publisher = publisher;

        public Task PublishStartAsync(string serialNumber, long processId, long productId, decimal amount, string? productName = null, string? unit = null, decimal? pricePerUnit = null, CancellationToken ct = default)
            => _publisher.PublishRequestAsync(serialNumber, MqttHandlerTypes.ProcessStart, new
            {
                process_id = processId,
                product_id = productId,
                amount,
                product_name = productName,
                unit,
                price_per_unit = pricePerUnit
            }, ct);

        public Task PublishStopAsync(string serialNumber, long processId, CancellationToken ct = default)
            => _publisher.PublishRequestAsync(serialNumber, MqttHandlerTypes.ProcessStop, new { process_id = processId }, ct);

        public Task PublishPauseAsync(string serialNumber, long processId, CancellationToken ct = default)
            => _publisher.PublishRequestAsync(serialNumber, MqttHandlerTypes.ProcessPause, new { process_id = processId }, ct);

        public Task PublishResumeAsync(string serialNumber, long processId, CancellationToken ct = default)
            => _publisher.PublishRequestAsync(serialNumber, MqttHandlerTypes.ProcessResume, new { process_id = processId }, ct);
    }
}
