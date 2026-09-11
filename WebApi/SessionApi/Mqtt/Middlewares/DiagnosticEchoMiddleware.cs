using System.Text;
using Microsoft.Extensions.Logging;
using MQTTnet.Protocol;
using SessionApi.Mqtt.Abstractions;
using SessionApi.Mqtt.Handlers;
using SessionApi.Mqtt.Topics;
using SessionApi.Mqtt.Transport;

namespace SessionApi.Mqtt.Middlewares
{
    /// <summary>
    /// <c>diag.echo</c> — MQTT transportini YAKKA O'ZINI tekshirish uchun. Hech qanday
    /// tekshiruv yo'q: HMAC ham, qurilma DB'da bor-yo'qligi ham, timestamp ham, replay ham
    /// tekshirilmaydi. Xabar keldi — javob ketdi.
    ///
    /// Maqsadi: "qurilma javob olmayapti" muammosida transport (broker, obuna, ACL, topic)
    /// bilan biznes qatlamini (kalit, pending sessiya, counter) BIR-BIRIDAN ajratish.
    /// Echo kelsa — yo'l ochiq, muammo yuqori qatlamda; kelmasa — muammo brokerda.
    ///
    /// Shuning uchun pipeline'ning ENG BOSHIDA, DeserializeMiddleware'dan keyin turadi va
    /// zanjirni qisqa tutashtiradi.
    ///
    /// <para><b>Javob IMZOLANMAYDI</b> (<c>hmac:""</c>): kalit noto'g'ri bo'lgan qurilma ham
    /// javobni o'qiy olishi kerak — aks holda aynan tekshirmoqchi bo'lgan holatda echo
    /// foydasiz bo'lardi. Javobda hech qanday maxfiy ma'lumot yo'q.</para>
    /// </summary>
    public sealed class DiagnosticEchoMiddleware : IMqttMiddleware
    {
        private readonly MqttConnection _connection;
        private readonly ILogger<DiagnosticEchoMiddleware> _logger;

        public DiagnosticEchoMiddleware(MqttConnection connection, ILogger<DiagnosticEchoMiddleware> logger)
        {
            _connection = connection;
            _logger = logger;
        }

        public async Task InvokeAsync(MqttContext context, MqttNext next)
        {
            if (context.Envelope is null || context.Envelope.Type != MqttHandlerTypes.DiagEcho)
            {
                await next();
                return;
            }

            var serverUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var skew = serverUnix - context.Envelope.Timestamp;

            _logger.LogInformation(
                "[diag.echo] serial={Serial} id={Id} kind={Kind} skew={Skew}s — javob qaytarilmoqda.",
                context.SerialNumber, context.Envelope.Id, context.TopicKind, skew);

            var payload = BuildPayload(context, serverUnix, skew);
            var json = BuildUnsignedEnvelope(context.Envelope.Id, MqttHandlerTypes.DiagEcho, serverUnix, payload);

            await _connection.PublishAsync(
                MqttTopics.ServerResponse(context.SerialNumber),
                json,
                MqttQualityOfServiceLevel.AtLeastOnce,
                context.CancellationToken);

            _logger.LogInformation(
                "[diag.echo] javob yuborildi topic={Topic} echoId={Id}",
                MqttTopics.ServerResponse(context.SerialNumber), context.Envelope.Id);

            // Zanjir shu yerda tugaydi — echo hech qanday biznes holatiga tegmaydi.
        }

        /// <summary>
        /// Javob payload'i: qurilma yuborganini qaytaradi + soat farqini aytadi.
        /// Hech qanday DB/Redis o'qish yo'q — echo hech narsaga bog'liq bo'lmasligi kerak.
        /// </summary>
        private static string BuildPayload(MqttContext context, long serverUnix, long skew)
        {
            var sb = new StringBuilder(context.Envelope!.PayloadJson.Length + 256);

            sb.Append("{\"ok\":true")
              .Append(",\"serial\":\"").Append(Escape(context.SerialNumber)).Append('"')
              .Append(",\"topic_kind\":\"").Append(context.TopicKind).Append('"')
              .Append(",\"received_id\":").Append(context.Envelope.Id)
              .Append(",\"received_timestamp\":").Append(context.Envelope.Timestamp)
              .Append(",\"server_unix\":").Append(serverUnix)
              // Musbat qiymat — qurilma soati orqada. |skew| > 60s bo'lsa oddiy xabarlar
              // TIMESTAMP_SKEW bilan rad etiladi, echo esa baribir ishlayveradi.
              .Append(",\"clock_skew_sec\":").Append(skew)
              .Append(",\"echo\":").Append(context.Envelope.PayloadJson)
              .Append('}');

            return sb.ToString();
        }

        /// <summary>
        /// Envelope'ning imzosiz varianti. ATAYLAB shu faylda va <c>private</c>: umumiy
        /// serializatorga qo'shilsa, kimdir uni oddiy xabarga ishlatib qo'yishi mumkin edi.
        /// </summary>
        private static string BuildUnsignedEnvelope(long id, string type, long timestamp, string payloadJson)
            => new StringBuilder(payloadJson.Length + 128)
                .Append("{\"id\":").Append(id)
                .Append(",\"type\":\"").Append(Escape(type)).Append('"')
                .Append(",\"timestamp\":").Append(timestamp)
                .Append(",\"payload\":").Append(payloadJson)
                .Append(",\"hmac\":\"\"}")
                .ToString();

        private static string Escape(string value)
            => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
