namespace SessionApi.Mqtt.Abstractions
{
    /// <summary>
    /// Standard response payload — server→device va device→server response topiclar uchun
    /// "payload" field ichida joylashadi. Format barcha command/response juftlar uchun bir xil.
    /// </summary>
    public sealed record MqttResponseEnvelope<T>(
        bool Ok,
        string Code,
        string Message,
        T? Data,
        DateTime Timestamp);

    public static class MqttResponseEnvelope
    {
        public static MqttResponseEnvelope<T> Success<T>(string code, string message, T data)
            => new(true, code, message, data, DateTime.Now);

        public static MqttResponseEnvelope<T> Fail<T>(string code, string message)
            => new(false, code, message, default, DateTime.Now);
    }
}
