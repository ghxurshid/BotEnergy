using SessionApi.Mqtt.Abstractions;

namespace SessionApi.Mqtt.Topics
{
    /// <summary>
    /// MQTT topic konvensiyalari va parserlar.
    ///
    /// <b>Topic struktura:</b>
    /// <list type="bullet">
    /// <item><c>device/{serial}/request</c>   — device → server request (response talab qiladi)</item>
    /// <item><c>device/{serial}/response</c>  — device → server, server request'iga javob</item>
    /// <item><c>device/{serial}/event</c>     — device → server fire-and-forget</item>
    /// <item><c>device/{serial}/telemetry</c> — device → server real-time data</item>
    /// <item><c>device/{serial}/state</c>     — device → server retained state snapshot</item>
    /// <item><c>server/{serial}/request</c>   — server → device request</item>
    /// <item><c>server/{serial}/response</c>  — server → device, device request'iga javob</item>
    /// </list>
    /// </summary>
    public static class MqttTopics
    {
        // Subscription wildcards (server boots — subscribe qiladi)
        public const string DeviceRequestSub = "device/+/request";
        public const string DeviceResponseSub = "device/+/response";
        public const string DeviceEventSub = "device/+/event";
        public const string DeviceTelemetrySub = "device/+/telemetry";
        public const string DeviceStateSub = "device/+/state";

        // Server → device (publish targets)
        public static string ServerRequest(string serial) => $"server/{serial}/request";
        public static string ServerResponse(string serial) => $"server/{serial}/response";

        /// <summary>
        /// "device/{serial}/{kind}" topic'ni parse qiladi.
        /// Topic noto'g'ri formatda bo'lsa <c>null</c> qaytaradi.
        /// </summary>
        public static ParsedTopic? Parse(string topic)
        {
            if (string.IsNullOrEmpty(topic)) return null;

            var parts = topic.Split('/');
            if (parts.Length != 3) return null;

            if (parts[0] != "device") return null;

            var serial = parts[1];
            if (string.IsNullOrEmpty(serial)) return null;

            var kind = parts[2] switch
            {
                "request" => MqttTopicKind.Request,
                "response" => MqttTopicKind.Response,
                "event" => MqttTopicKind.Event,
                "telemetry" => MqttTopicKind.Telemetry,
                "state" => MqttTopicKind.State,
                _ => (MqttTopicKind?)null
            };

            if (kind is null) return null;

            return new ParsedTopic(serial, kind.Value);
        }
    }

    public sealed record ParsedTopic(string SerialNumber, MqttTopicKind Kind);
}
