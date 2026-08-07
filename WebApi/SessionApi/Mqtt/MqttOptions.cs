namespace SessionApi.Mqtt
{
    public class MqttOptions
    {
        public string BrokerHost { get; set; } = "localhost";
        public int BrokerPort { get; set; } = 1883;
        public string? Username { get; set; }
        public string? Password { get; set; }
        /// <summary>
        /// ClientId <b>prefiksi</b>. Haqiqiy ClientId ga instansiya identifikatori qo'shiladi
        /// (<see cref="EffectiveClientId"/>) — MQTT spetsifikatsiyasi bo'yicha bir xil ClientId
        /// bilan ikkinchi ulanish birinchisini uzadi, ya'ni sobit ClientId bilan SessionApi'ni
        /// ikkinchi replikaga chiqarish mumkin emas edi.
        /// </summary>
        public string ClientId { get; set; } = "botenergy-session-api";

        /// <summary>
        /// Shared subscription guruhi (MQTT 5 / EMQX <c>$share/{group}/{topic}</c>).
        /// To'ldirilgan bo'lsa inbound obunalar shu guruh orqali qilinadi va xabarlar
        /// replikalar orasida <b>taqsimlanadi</b> (har biri hamma xabarni olmaydi).
        ///
        /// Bo'sh qoldirilsa oddiy obuna ishlatiladi — Development va shared subscription'ni
        /// qo'llab-quvvatlamaydigan brokerlar (Mosquitto) uchun.
        ///
        /// Diqqat: <c>state</c> topic'i retained snapshot bo'lgani uchun HAR BIR instansiyaga
        /// kerak — u hech qachon shared qilinmaydi.
        /// </summary>
        public string? SharedSubscriptionGroup { get; set; }

        /// <summary>
        /// Brokerga beriladigan yakuniy ClientId — prefiks + mashina nomi + process id.
        /// Bir xil image'dan ko'tarilgan ikki konteyner ham turlicha qiymat oladi.
        /// </summary>
        public string EffectiveClientId =>
            $"{ClientId}-{Environment.MachineName}-{Environment.ProcessId}";

        /// <summary>
        /// Topic'ni obuna uchun tayyorlaydi: shared guruh berilgan bo'lsa <c>$share/{group}/</c>
        /// prefiksini qo'shadi.
        /// </summary>
        public string SubscriptionTopic(string topic) =>
            string.IsNullOrWhiteSpace(SharedSubscriptionGroup)
                ? topic
                : $"$share/{SharedSubscriptionGroup}/{topic}";

        // ── TLS (transport-level) ──────────────────────────────────────
        public bool UseTls { get; set; } = false;
        public bool AllowUntrustedCertificates { get; set; } = false;
        public string? ClientCertificatePath { get; set; }
        public string? ClientCertificatePassword { get; set; }
    }
}
