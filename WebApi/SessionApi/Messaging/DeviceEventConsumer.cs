using CommonConfiguration.Messaging;
using Domain.Dtos.Process;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Messaging;
using Domain.Messaging.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SessionApi.Messaging
{
    /// <summary>
    /// SessionApi MqttBridge tomonidan RabbitMQ EventQueue'ga yuborilgan qurilma eventlarini iste'mol qiladi.
    /// Oqim: Qurilma → MQTT → MqttBridge → RabbitMQ → bu consumer → SessionService/ProcessService → SignalR.
    /// Connected event'da sessiya allaqachon MqttBridge (DeviceSessionService) tomonidan DB'da yaratilgan —
    /// bu consumer faqat SignalR push qiladi.
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

                // Telemetriya endi RabbitMQ orqali emas — MqttBridge ProcessService'ni
                // to'g'ridan-to'g'ri chaqiradi (real-time latency minimum'ga tushiriladi).

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

            // Sessiya allaqachon DeviceApi tomonidan DB'da Connected statusda yaratilgan —
            // bu yerda faqat SignalR orqali mobile'ga push qilamiz.
            var result = await sessionService.NotifyDeviceConnectedAsync(e.SessionToken ?? string.Empty);
            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "NotifyDeviceConnected muvaffaqiyatsiz: SessionToken=*** Serial={Serial} — {Error}",
                    e.SerialNumber, result.ErrorObj?.ErrorMessage);
            }
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
                TotalGiven = e.TotalGiven ?? 0,
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
