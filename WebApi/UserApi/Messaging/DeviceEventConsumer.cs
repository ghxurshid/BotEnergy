using CommonConfiguration.Messaging;
using Domain.Dtos.Session;
using Domain.Interfaces;
using Domain.Messaging;
using Domain.Messaging.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace UserApi.Messaging
{
    /// <summary>
    /// DeviceApi dan RabbitMQ orqali kelgan eventlarni qayta ishlaydi.
    /// EventQueue: Qurilma → MQTT → DeviceApi → RabbitMQ → UserApi → SessionService → SignalR
    /// </summary>
    public sealed class DeviceEventConsumer : RabbitMqConsumerBase<DeviceEvent>
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DeviceEventConsumer> _logger;

        public DeviceEventConsumer(
            RabbitMqConnectionManager connectionManager,
            IServiceScopeFactory scopeFactory,
            ILogger<DeviceEventConsumer> logger)
            : base(connectionManager, logger, QueueNames.EventQueue)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task HandleMessageAsync(DeviceEvent deviceEvent)
        {
            _logger.LogInformation(
                "RabbitMQ event qabul qilindi: {Type} — Serial: {Serial}",
                deviceEvent.EventType, deviceEvent.SerialNumber);

            switch (deviceEvent.EventType)
            {
                case DeviceEventTypes.Connected:
                    await HandleDeviceConnectedAsync(deviceEvent);
                    break;

                case DeviceEventTypes.Telemetry:
                    await HandleTelemetryAsync(deviceEvent);
                    break;

                case DeviceEventTypes.Completed:
                    await HandleSessionCompletedAsync(deviceEvent);
                    break;

                case DeviceEventTypes.Status:
                    _logger.LogInformation(
                        "Device {Serial} status: {Payload}",
                        deviceEvent.SerialNumber, deviceEvent.StatusPayload);
                    break;

                default:
                    _logger.LogWarning("Noma'lum event turi: {Type}", deviceEvent.EventType);
                    break;
            }
        }

        private async Task HandleDeviceConnectedAsync(DeviceEvent e)
        {
            using var scope = _scopeFactory.CreateScope();
            var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();

            await sessionService.DeviceConnectAsync(new DeviceConnectedDto
            {
                SessionToken = e.SessionToken ?? string.Empty,
                SerialNumber = e.SerialNumber
            });
        }

        private async Task HandleTelemetryAsync(DeviceEvent e)
        {
            using var scope = _scopeFactory.CreateScope();
            var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();

            await sessionService.ReportProgressAsync(new SessionProgressDto
            {
                SessionToken = e.SessionToken ?? string.Empty,
                SerialNumber = e.SerialNumber,
                Quantity = e.Quantity ?? 0
            });
        }

        private async Task HandleSessionCompletedAsync(DeviceEvent e)
        {
            using var scope = _scopeFactory.CreateScope();
            var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();

            await sessionService.DeviceFinishAsync(new DeviceFinishDto
            {
                SessionToken = e.SessionToken ?? string.Empty,
                SerialNumber = e.SerialNumber,
                FinalQuantity = e.FinalQuantity ?? 0,
                EndReason = e.EndReason
            });
        }
    }
}
