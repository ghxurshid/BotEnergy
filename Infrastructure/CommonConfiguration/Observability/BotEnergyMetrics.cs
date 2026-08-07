using System.Diagnostics.Metrics;

namespace CommonConfiguration.Observability
{
    /// <summary>
    /// Biznes va MQTT pipeline metrikalari. Prometheus'da <c>botenergy_*</c> prefiksi bilan chiqadi.
    ///
    /// Bu metrikalar oltin signallardan (latency/error/traffic) farqli — ular
    /// "tizim texnik jihatdan sog'mi" emas, "biznes to'g'ri ishlayaptimi" degan savolga javob beradi.
    /// Masalan, <see cref="MqttRejected"/> ning <c>reason=hmac</c> bo'yicha o'sishi hujum yoki
    /// noto'g'ri firmware belgisidir — HTTP xato darajasida u umuman ko'rinmaydi.
    /// </summary>
    public static class BotEnergyMetrics
    {
        /// <summary>Pipeline'ga kirgan MQTT xabarlari (topic turi bo'yicha).</summary>
        public static readonly Counter<long> MqttReceived =
            ObservabilityExtensions.Meter.CreateCounter<long>(
                "botenergy_mqtt_received_total",
                unit: "messages",
                description: "Brokerdan qabul qilingan MQTT xabarlari soni");

        /// <summary>Pipeline tomonidan rad etilgan xabarlar (sabab bo'yicha).</summary>
        public static readonly Counter<long> MqttRejected =
            ObservabilityExtensions.Meter.CreateCounter<long>(
                "botenergy_mqtt_rejected_total",
                unit: "messages",
                description: "Pipeline rad etgan MQTT xabarlari (reason: deserialize|device|hmac|timestamp|replay|dispatch)");

        /// <summary>Handler tomonidan muvaffaqiyatli ishlangan xabarlar.</summary>
        public static readonly Counter<long> MqttHandled =
            ObservabilityExtensions.Meter.CreateCounter<long>(
                "botenergy_mqtt_handled_total",
                unit: "messages",
                description: "Handler'gacha yetib borgan va ishlangan MQTT xabarlari");

        /// <summary>Qurilmaga yuborilgan buyruqlar.</summary>
        public static readonly Counter<long> MqttPublished =
            ObservabilityExtensions.Meter.CreateCounter<long>(
                "botenergy_mqtt_published_total",
                unit: "messages",
                description: "Serverdan qurilmaga yuborilgan MQTT xabarlari");

        /// <summary>Pipeline ishlash vaqti.</summary>
        public static readonly Histogram<double> MqttPipelineDuration =
            ObservabilityExtensions.Meter.CreateHistogram<double>(
                "botenergy_mqtt_pipeline_duration_ms",
                unit: "ms",
                description: "MQTT pipeline'ning to'liq ishlash vaqti");

        public static void RecordReceived(string topicKind) =>
            MqttReceived.Add(1, new KeyValuePair<string, object?>("kind", topicKind));

        public static void RecordRejected(string reason, string topicKind) =>
            MqttRejected.Add(1,
                new KeyValuePair<string, object?>("reason", reason),
                new KeyValuePair<string, object?>("kind", topicKind));

        public static void RecordHandled(string messageType) =>
            MqttHandled.Add(1, new KeyValuePair<string, object?>("type", messageType));

        public static void RecordPublished(string topicKind) =>
            MqttPublished.Add(1, new KeyValuePair<string, object?>("kind", topicKind));
    }
}
