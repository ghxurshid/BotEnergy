namespace SessionApi.Mqtt.Abstractions
{
    public delegate Task MqttNext();

    public interface IMqttMiddleware
    {
        Task InvokeAsync(MqttContext context, MqttNext next);
    }
}
