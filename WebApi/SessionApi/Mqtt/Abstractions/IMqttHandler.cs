namespace SessionApi.Mqtt.Abstractions
{
    /// <summary>
    /// Non-generic handler entry-point invoked by the dispatcher. Concrete handlers inherit from
    /// <see cref="MqttCommandHandler{TPayload,TResponse}"/> or <see cref="MqttEventHandler{TPayload}"/>,
    /// which deserialize the raw payload JSON into the strongly-typed DTO.
    /// </summary>
    public interface IMqttHandler
    {
        Task<object?> HandleAsync(string payloadJson, MqttContext context);
    }
}
