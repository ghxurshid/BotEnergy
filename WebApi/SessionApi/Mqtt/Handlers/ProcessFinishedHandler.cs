using Domain.Dtos.Process;
using Domain.Enums;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using SessionApi.Mqtt.Abstractions;

namespace SessionApi.Mqtt.Handlers
{
    /// <summary>
    /// <c>device/{serial}/event</c>, <c>type=process.finished</c> — jarayon yakunlanishi
    /// (stopped/error/out_of_resource/completed). <see cref="IProcessService.ReportDeviceFinishedAsync"/>
    /// to'g'ridan-to'g'ri chaqiriladi — oraliq RabbitMQ hop yo'q.
    /// </summary>
    [MqttHandler(MqttHandlerTypes.ProcessFinished, MqttTopicKind.Event)]
    public sealed class ProcessFinishedHandler : MqttEventHandler<ProcessFinishedHandler.Payload>
    {
        private readonly IProcessService _processService;
        private readonly ILogger<ProcessFinishedHandler> _logger;

        public ProcessFinishedHandler(IProcessService processService, ILogger<ProcessFinishedHandler> logger)
        {
            _processService = processService;
            _logger = logger;
        }

        protected override async Task HandleAsync(Payload payload, MqttContext context)
        {
            var reason = (payload.Reason ?? string.Empty).ToLowerInvariant();
            if (reason is not ("stopped" or "error" or "out_of_resource" or "completed"))
            {
                _logger.LogWarning(
                    "[process.finished] Noma'lum reason={Reason} serial={Serial}",
                    payload.Reason, context.SerialNumber);
                return;
            }

            await _processService.ReportDeviceFinishedAsync(new DeviceProcessReportDto
            {
                SessionToken = payload.SessionToken ?? string.Empty,
                SerialNumber = context.SerialNumber,
                ProcessId = payload.ProcessId ?? 0,
                TotalGiven = payload.TotalGiven ?? 0,
                EndReason = MapEndReason(reason)
            });
        }

        private static ProcessEndReason MapEndReason(string raw) => raw switch
        {
            "completed" => ProcessEndReason.Completed,
            "stopped" => ProcessEndReason.UserStopped,
            "out_of_resource" => ProcessEndReason.OutOfResource,
            _ => ProcessEndReason.DeviceError
        };

        public sealed class Payload
        {
            /// <summary>stopped | error | out_of_resource | completed</summary>
            public string? Reason { get; set; }
            public string? SessionToken { get; set; }
            public long? ProcessId { get; set; }
            public decimal? TotalGiven { get; set; }
        }
    }
}
