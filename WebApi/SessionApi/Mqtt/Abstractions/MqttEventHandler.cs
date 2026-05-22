using System.Text.Json;

namespace SessionApi.Mqtt.Abstractions
{
    /// <summary>
    /// Base class for fire-and-forget handlers (events, telemetry, state). No response is published.
    /// </summary>
    public abstract class MqttEventHandler<TPayload> : IMqttHandler
    {
        public async Task<object?> HandleAsync(string payloadJson, MqttContext context)
        {
            TPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<TPayload>(payloadJson, MqttEnvelopeSerializer.JsonOpts);
            }
            catch (JsonException)
            {
                return null;
            }

            if (payload is null) return null;

            await HandleAsync(payload, context);
            return null;
        }

        protected abstract Task HandleAsync(TPayload payload, MqttContext context);
    }
}
