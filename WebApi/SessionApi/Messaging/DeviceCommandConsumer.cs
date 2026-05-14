using CommonConfiguration.Messaging;
using DeviceApi.Mqtt;
using Domain.Messaging;
using Domain.Messaging.Commands;
using Microsoft.Extensions.Logging;

namespace DeviceApi.Messaging
{
    /// <summary>
    /// UserApi dan RabbitMQ orqali kelgan buyruqlarni MQTT orqali qurilmaga yuboradi.
    /// CommandQueue: UserApi → RabbitMQ → DeviceApi → MQTT → Qurilma
    /// </summary>
    public sealed class DeviceCommandConsumer : RabbitMqConsumerBase<DeviceCommand>
    {
        private readonly MqttBridge _mqttBridge;
        private readonly ILogger<DeviceCommandConsumer> _logger;

        public DeviceCommandConsumer(
            RabbitMqConnectionManager connectionManager,
            MqttBridge mqttBridge,
            ILogger<DeviceCommandConsumer> logger)
            : base(connectionManager, logger, QueueNames.CommandQueue)
        {
            _mqttBridge = mqttBridge;
            _logger = logger;
        }

        protected override async Task HandleMessageAsync(DeviceCommand command)
        {
            _logger.LogInformation(
                "RabbitMQ buyruq qabul qilindi: {Type} → {Serial} (process={Process})",
                command.CommandType, command.SerialNumber, command.ProcessId);

            switch (command.CommandType)
            {
                case DeviceCommandTypes.Start:
                    await _mqttBridge.PublishStartCommandAsync(
                        command.SerialNumber,
                        command.ProcessId,
                        command.ProductId ?? 0,
                        command.Amount ?? 0);
                    break;

                case DeviceCommandTypes.Pause:
                    await _mqttBridge.PublishPauseCommandAsync(command.SerialNumber, command.ProcessId);
                    break;

                case DeviceCommandTypes.Resume:
                    await _mqttBridge.PublishResumeCommandAsync(command.SerialNumber, command.ProcessId);
                    break;

                case DeviceCommandTypes.Stop:
                    await _mqttBridge.PublishStopCommandAsync(command.SerialNumber, command.ProcessId);
                    break;

                default:
                    _logger.LogWarning("Noma'lum buyruq turi: {Type}", command.CommandType);
                    break;
            }
        }
    }
}
