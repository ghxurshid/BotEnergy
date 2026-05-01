using CommonConfiguration.Messaging;
using Domain.Interfaces;
using Domain.Messaging;
using Domain.Messaging.Commands;

namespace UserApi.Messaging
{
    /// <summary>
    /// IDeviceCommandPublisher ning RabbitMQ implementatsiyasi.
    /// Service qatlami buni chaqiradi — RabbitMQ → DeviceApi → MQTT zanjirini ishga tushiradi.
    /// </summary>
    public sealed class RabbitMqDeviceCommandPublisher : IDeviceCommandPublisher
    {
        private readonly RabbitMqPublisher _publisher;

        public RabbitMqDeviceCommandPublisher(RabbitMqPublisher publisher)
            => _publisher = publisher;

        public void PublishStart(string serialNumber, long processId, long productId, decimal amount)
            => Send(DeviceCommandTypes.Start, serialNumber, processId, productId, amount);

        public void PublishStop(string serialNumber, long processId)
            => Send(DeviceCommandTypes.Stop, serialNumber, processId);

        public void PublishPause(string serialNumber, long processId)
            => Send(DeviceCommandTypes.Pause, serialNumber, processId);

        public void PublishResume(string serialNumber, long processId)
            => Send(DeviceCommandTypes.Resume, serialNumber, processId);

        private void Send(string type, string serialNumber, long processId, long? productId = null, decimal? amount = null)
        {
            _publisher.Publish(QueueNames.CommandQueue, new DeviceCommand
            {
                CommandType = type,
                SerialNumber = serialNumber,
                ProcessId = processId,
                ProductId = productId,
                Amount = amount
            });
        }
    }
}
