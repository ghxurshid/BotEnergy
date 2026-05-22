using System.Reflection;
using SessionApi.Mqtt.Abstractions;

namespace SessionApi.Mqtt.Dispatching
{
    /// <summary>
    /// Startup'da <see cref="MqttHandlerAttribute"/> bilan belgilangan handler turlarini
    /// reflection orqali topadi va (TopicKind, Type) → HandlerType xaritasini yig'adi.
    /// </summary>
    public sealed class MqttHandlerRegistry
    {
        private readonly Dictionary<(MqttTopicKind, string), Type> _map = new();

        public MqttHandlerRegistry(Assembly handlerAssembly)
        {
            foreach (var t in handlerAssembly.GetTypes())
            {
                var attr = t.GetCustomAttribute<MqttHandlerAttribute>();
                if (attr is null) continue;
                if (!typeof(IMqttHandler).IsAssignableFrom(t)) continue;
                if (t.IsAbstract) continue;

                var key = (attr.TopicKind, attr.Type);
                if (_map.ContainsKey(key))
                    throw new InvalidOperationException(
                        $"Bir nechta MQTT handler bir xil (TopicKind={attr.TopicKind}, Type={attr.Type}) uchun: " +
                        $"{_map[key].FullName} va {t.FullName}");

                _map[key] = t;
            }
        }

        public IEnumerable<Type> RegisteredHandlerTypes => _map.Values;

        public Type? Resolve(MqttTopicKind topicKind, string type)
            => _map.TryGetValue((topicKind, type), out var handlerType) ? handlerType : null;
    }
}
