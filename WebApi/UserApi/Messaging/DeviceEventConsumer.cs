using CommonConfiguration.Messaging;
using Domain.Dtos.Process;
using Domain.Dtos.Session;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Messaging;
using Domain.Messaging.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace UserApi.Messaging
{
    /// <summary>
    /// DeviceApi dan RabbitMQ orqali kelgan eventlarni qayta ishlaydi.
    /// EventQueue: Qurilma → MQTT → DeviceApi → RabbitMQ → UserApi → SessionService/ProcessService → SignalR
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
                "RabbitMQ event qabul qilindi: {Type} — Serial: {Serial} Process: {Process}",
                deviceEvent.EventType, deviceEvent.SerialNumber, deviceEvent.ProcessId);

            switch (deviceEvent.EventType)
            {
                case DeviceEventTypes.Connected:
                    await HandleDeviceConnectedAsync(deviceEvent);
                    break;

                case DeviceEventTypes.Telemetry:
                    await HandleTelemetryAsync(deviceEvent);
                    break;

                case DeviceEventTypes.Finished:
                    await HandleFinishedAsync(deviceEvent);
                    break;

                case DeviceEventTypes.Heartbeat:
                    // LastSeenAt allaqachon DeviceApi tomonidan yangilangan — bu yerda qo'shimcha hech narsa qilmaymiz.
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
            var processService = scope.ServiceProvider.GetRequiredService<IProcessService>();

            await processService.ReportTelemetryAsync(new ProcessTelemetryDto
            {
                SessionToken = e.SessionToken ?? string.Empty,
                SerialNumber = e.SerialNumber,
                ProcessId = e.ProcessId ?? 0,
                Quantity = e.Quantity ?? 0,
                Sequence = e.Sequence ?? 0
            });
        }

        private async Task HandleFinishedAsync(DeviceEvent e)
        {
            using var scope = _scopeFactory.CreateScope();
            var processService = scope.ServiceProvider.GetRequiredService<IProcessService>();

            await processService.ReportDeviceFinishedAsync(new DeviceProcessReportDto
            {
                SessionToken = e.SessionToken ?? string.Empty,
                SerialNumber = e.SerialNumber,
                ProcessId = e.ProcessId ?? 0,
                FinalQuantity = e.FinalQuantity ?? 0,
                EndReason = MapEndReason(e.EndReason)
            });
        }

        private static ProcessEndReason MapEndReason(string? raw) => raw?.ToLowerInvariant() switch
        {
            "completed" => ProcessEndReason.Completed,
            "stopped" => ProcessEndReason.UserStopped,
            "out_of_resource" => ProcessEndReason.OutOfResource,
            _ => ProcessEndReason.DeviceError
        };
    }
}
