using Domain.Dtos.Process;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using SessionApi.Mqtt.Abstractions;

namespace SessionApi.Mqtt.Handlers
{
    /// <summary>
    /// <c>device/{serial}/event</c>, <c>type=process.paused</c> — qurilma pauza buyrug'ini
    /// inersiya bilan yakunlab, oqim to'liq to'xtaganini tasdiqlaydi.
    /// <see cref="IProcessService.ReportDevicePausedAsync"/> to'g'ridan-to'g'ri chaqiriladi.
    /// </summary>
    [MqttHandler(MqttHandlerTypes.ProcessPaused, MqttTopicKind.Event)]
    public sealed class ProcessPausedHandler : MqttEventHandler<ProcessPausedHandler.Payload>
    {
        private readonly IProcessService _processService;
        private readonly ILogger<ProcessPausedHandler> _logger;

        public ProcessPausedHandler(IProcessService processService, ILogger<ProcessPausedHandler> logger)
        {
            _processService = processService;
            _logger = logger;
        }

        protected override async Task HandleAsync(Payload payload, MqttContext context)
        {
            var result = await _processService.ReportDevicePausedAsync(new DeviceProcessPausedDto
            {
                SessionToken = payload.SessionToken ?? string.Empty,
                SerialNumber = context.SerialNumber,
                ProcessId = payload.ProcessId ?? 0,
                TotalGiven = payload.TotalGiven ?? 0
            });

            if (!result.IsSuccess)
                _logger.LogWarning(
                    "[process.paused] rad etildi serial={Serial} process={Process} code={Code} msg={Msg}",
                    context.SerialNumber, payload.ProcessId, result.ErrorObj?.Code, result.ErrorObj?.ErrorMessage);
        }

        public sealed class Payload
        {
            public string? SessionToken { get; set; }
            public long? ProcessId { get; set; }
            public decimal? TotalGiven { get; set; }
        }
    }
}
