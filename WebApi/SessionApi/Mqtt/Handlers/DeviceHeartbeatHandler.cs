using SessionApi.Mqtt.Abstractions;

namespace SessionApi.Mqtt.Handlers
{
    /// <summary>
    /// <c>device/{serial}/event</c>, <c>type=device.heartbeat</c>.
    /// LastSeenAt allaqachon <c>MqttHost.OnMessageAsync</c> tomonidan har auth-o'tgan xabarda
    /// yangilanadi — bu handler shu sababli no-op. Mavjudligi shunchaki dispatcher'da
    /// "handler topilmadi" warning'ini oldini olish uchun.
    /// </summary>
    [MqttHandler(MqttHandlerTypes.DeviceHeartbeat, MqttTopicKind.Event)]
    public sealed class DeviceHeartbeatHandler : MqttEventHandler<DeviceHeartbeatHandler.Payload>
    {
        protected override Task HandleAsync(Payload payload, MqttContext context) => Task.CompletedTask;

        public sealed class Payload { }
    }
}
