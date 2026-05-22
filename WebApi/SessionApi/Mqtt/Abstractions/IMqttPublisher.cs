namespace SessionApi.Mqtt.Abstractions
{
    /// <summary>
    /// Server → device MQTT publisher. Transport qatlamida implement qilinadi
    /// (<c>MqttPublisher</c>). Handler/middleware'lar shu interface orqali ishlatadi.
    ///
    /// Generic ishlatilmaydi — payload <c>object</c>, JSON serializer runtime turidan
    /// foydalanadi. Bu dispatcher'da reflection/dynamic'siz response yuborishga imkon beradi.
    /// </summary>
    public interface IMqttPublisher
    {
        /// <summary>
        /// Server'ning <c>IMqttMessageIdStore</c> dan keyingi outbound id'sini olib
        /// <c>server/{serial}/request</c> ga publish qiladi.
        /// </summary>
        Task PublishRequestAsync(string serialNumber, string type, object payload, CancellationToken ct = default);

        /// <summary>
        /// Device request'iga javob — <paramref name="correlationId"/> echo qilinadi.
        /// <c>server/{serial}/response</c> ga publish qilinadi.
        /// <paramref name="response"/> odatda <c>MqttResponseEnvelope&lt;T&gt;</c> bo'ladi.
        /// </summary>
        Task PublishResponseAsync(string serialNumber, long correlationId, string type, object response, CancellationToken ct = default);
    }
}
