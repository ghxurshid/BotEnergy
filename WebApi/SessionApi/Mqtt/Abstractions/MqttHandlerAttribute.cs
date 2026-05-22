namespace SessionApi.Mqtt.Abstractions
{
    /// <summary>
    /// Marks a class as an MQTT handler for a specific envelope <c>type</c> arriving on a specific topic kind.
    /// Discovered at startup via reflection and registered in <c>MqttHandlerRegistry</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class MqttHandlerAttribute : Attribute
    {
        public string Type { get; }
        public MqttTopicKind TopicKind { get; }

        public MqttHandlerAttribute(string type, MqttTopicKind topicKind)
        {
            Type = type;
            TopicKind = topicKind;
        }
    }

    public enum MqttTopicKind
    {
        Request,
        Response,
        Event,
        Telemetry,
        State
    }
}
