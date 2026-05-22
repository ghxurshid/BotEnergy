using Domain.Entities;

namespace SessionApi.Mqtt.Abstractions
{
    /// <summary>
    /// Per-message context flowing through the middleware pipeline. Mirrors ASP.NET's HttpContext
    /// in spirit: middlewares mutate it (e.g. AuthMiddleware sets <see cref="Device"/>), the
    /// dispatcher consumes the final state.
    /// </summary>
    public sealed class MqttContext
    {
        public required string Topic { get; init; }
        public required string SerialNumber { get; init; }
        public required MqttTopicKind TopicKind { get; init; }
        public required string RawJson { get; init; }
        public required IServiceProvider Services { get; init; }
        public required CancellationToken CancellationToken { get; init; }

        public MqttEnvelope? Envelope { get; set; }
        public DeviceEntity? Device { get; set; }

        public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Set by command handlers via the dispatcher. After the pipeline completes, the host
        /// publishes this to <c>server/{serial}/response</c> with the inbound envelope's id echoed.
        /// </summary>
        public object? Response { get; set; }
    }
}
