using System.Text.Json;

namespace SessionApi.Mqtt.Abstractions
{
    /// <summary>
    /// Base class for request handlers — handler returns a response envelope which the dispatcher
    /// publishes to the corresponding response topic (request id is echoed for correlation).
    /// </summary>
    public abstract class MqttCommandHandler<TPayload, TResponse> : IMqttHandler
    {
        public async Task<object?> HandleAsync(string payloadJson, MqttContext context)
        {
            TPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<TPayload>(payloadJson, MqttEnvelopeSerializer.JsonOpts);
            }
            catch (JsonException ex)
            {
                return MqttResponseEnvelope.Fail<TResponse>(
                    MqttResultCodes.InvalidPayload,
                    $"Payload JSON parse xatosi: {ex.Message}");
            }

            if (payload is null)
            {
                return MqttResponseEnvelope.Fail<TResponse>(
                    MqttResultCodes.InvalidPayload,
                    "Payload bo'sh.");
            }

            return await HandleAsync(payload, context);
        }

        protected abstract Task<MqttResponseEnvelope<TResponse>> HandleAsync(TPayload payload, MqttContext context);
    }
}
