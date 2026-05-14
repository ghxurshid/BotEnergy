namespace DeviceApi.Mqtt
{
    /// <summary>
    /// Qurilmaga yuboriladigan har qanday MQTT javobi uchun standart shablon.
    /// Format barcha topic'lar uchun bir xil: <c>{ ok, code, message, data, timestamp }</c>.
    ///
    /// <para><b>Maydonlar</b>:</para>
    /// <list type="bullet">
    /// <item><c>ok</c> — operatsiya muvaffaqiyatli (true) yoki xato (false).</item>
    /// <item><c>code</c> — qisqa machine-readable kod (masalan, "TOKEN_MISMATCH"). Qurilma displeyida shu kod bo'yicha branching qiladi.</item>
    /// <item><c>message</c> — odamga tushunarli xabar (debug log va displey uchun).</item>
    /// <item><c>data</c> — topic'ga xos payload (success holatda to'ldiriladi, fail'da null).</item>
    /// <item><c>timestamp</c> — javob yaratilgan vaqt (DateTime.Now, local).</item>
    /// </list>
    /// </summary>
    public sealed record MqttResultEnvelope<T>(
        bool Ok,
        string Code,
        string Message,
        T? Data,
        DateTime Timestamp);

    public static class MqttResultEnvelope
    {
        public static MqttResultEnvelope<T> Success<T>(string code, string message, T data)
            => new(true, code, message, data, DateTime.Now);

        public static MqttResultEnvelope<T> Fail<T>(string code, string message)
            => new(false, code, message, default, DateTime.Now);
    }
}
