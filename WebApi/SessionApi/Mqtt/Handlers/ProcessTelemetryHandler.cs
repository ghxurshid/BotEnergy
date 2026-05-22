using Domain.Dtos.Process;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using SessionApi.Mqtt.Abstractions;

namespace SessionApi.Mqtt.Handlers
{
    /// <summary>
    /// <c>device/{serial}/telemetry</c>, <c>type=process.telemetry</c>.
    /// Eski <c>MqttBridge.HandleTelemetryAsync</c> ning to'g'ridan-to'g'ri ekvivalenti — RabbitMQ orqali emas,
    /// shu process ichida <see cref="IProcessService.ReportTelemetryAsync"/> chaqiriladi (real-time SignalR push).
    /// </summary>
    [MqttHandler(MqttHandlerTypes.ProcessTelemetry, MqttTopicKind.Telemetry)]
    public sealed class ProcessTelemetryHandler : MqttEventHandler<ProcessTelemetryHandler.Payload>
    {
        private readonly IProcessService _processService;
        private readonly ILogger<ProcessTelemetryHandler> _logger;

        public ProcessTelemetryHandler(IProcessService processService, ILogger<ProcessTelemetryHandler> logger)
        {
            _processService = processService;
            _logger = logger;
        }

        protected override async Task HandleAsync(Payload payload, MqttContext context)
        {
            if (string.IsNullOrEmpty(payload.SessionToken) || payload.ProcessId <= 0)
            {
                _logger.LogWarning(
                    "[telemetry] Yaroqsiz payload serial={Serial} processId={Pid} tokenEmpty={Empty}",
                    context.SerialNumber, payload.ProcessId, string.IsNullOrEmpty(payload.SessionToken));
                return;
            }

            await _processService.ReportTelemetryAsync(new ProcessTelemetryDto
            {
                SessionToken = payload.SessionToken,
                SerialNumber = context.SerialNumber,
                ProcessId = payload.ProcessId,
                TotalGiven = payload.TotalGiven,
                Sequence = payload.Sequence
            });
        }

        /// <summary>
        /// <c>total_given</c> — jarayon boshidan beri qurilma jami bergan miqdor (cumulative).
        /// </summary>
        public sealed class Payload
        {
            public string SessionToken { get; set; } = string.Empty;
            public long ProcessId { get; set; }
            public long Sequence { get; set; }
            public decimal TotalGiven { get; set; }
        }
    }
}
