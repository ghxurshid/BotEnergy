using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SessionApi.Mqtt.Abstractions;

namespace SessionApi.Mqtt.Middlewares
{
    /// <summary>
    /// Pipeline rad etgan <c>request</c> xabariga xato javobini qaytaradi.
    ///
    /// Jim tashlab yuborish qurilmani 10 soniyalik <c>ack-timeout</c>ga tashlaydi va sababni
    /// unga ham, ekran oldidagi odamga ham aytmaydi — natijada "ulanmadi" dan boshqa hech qanday
    /// ma'lumot qolmaydi. Kodlar (<see cref="MqttResultCodes"/>) allaqachon mavjud edi, faqat
    /// yuborilmasdi.
    ///
    /// <b>Faqat HMAC tekshiruvidan O'TGAN</b> xabarlarga javob beriladi. HMAC yiqilganda javob
    /// bermaymiz: jo'natuvchi haqiqiy qurilma ekani noma'lum, qolaversa u bizning imzomizni
    /// tekshira olmaydi (kaliti boshqa) — javob baribir rad etilardi. U holat server logida
    /// <c>sabab=KeyOrContent</c> bo'lib qoladi.
    /// </summary>
    internal static class MqttRejectionResponder
    {
        public static async Task RespondAsync(MqttContext context, ILogger logger, string code, string message)
        {
            // Event/telemetry/state topiclari javob kutmaydi — ularga yozish faqat shovqin.
            if (context.TopicKind != MqttTopicKind.Request || context.Envelope is null)
                return;

            try
            {
                var publisher = context.Services.GetRequiredService<IMqttPublisher>();

                await publisher.PublishResponseAsync(
                    context.SerialNumber,
                    correlationId: context.Envelope.Id,
                    type: context.Envelope.Type,
                    response: MqttResponseEnvelope.Fail<object>(code, message),
                    ct: context.CancellationToken);
            }
            catch (Exception ex)
            {
                // Javob yuborilmasa qurilma eski xulqqa (ack-timeout) tushadi — bu xato
                // kiruvchi xabarni qayta ishlashni to'xtatmasligi kerak.
                logger.LogWarning(ex,
                    "[MQTT-OUT] Rad javobi yuborilmadi serial={Serial} code={Code}",
                    context.SerialNumber, code);
            }
        }
    }
}
