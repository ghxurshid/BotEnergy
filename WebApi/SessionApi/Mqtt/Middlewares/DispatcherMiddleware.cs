using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SessionApi.Mqtt.Abstractions;
using SessionApi.Mqtt.Dispatching;

namespace SessionApi.Mqtt.Middlewares
{
    /// <summary>
    /// Pipeline'ning terminal qadami. Envelope.Type va TopicKind bo'yicha handlerni topadi,
    /// invoke qiladi. Agar handler response qaytarsa — <see cref="IMqttPublisher"/> orqali
    /// <c>server/{serial}/response</c> ga publish qiladi (request id echo qilinadi).
    /// </summary>
    public sealed class DispatcherMiddleware : IMqttMiddleware
    {
        private readonly MqttHandlerRegistry _registry;
        private readonly IMqttPublisher _publisher;
        private readonly ILogger<DispatcherMiddleware> _logger;

        public DispatcherMiddleware(
            MqttHandlerRegistry registry,
            IMqttPublisher publisher,
            ILogger<DispatcherMiddleware> logger)
        {
            _registry = registry;
            _publisher = publisher;
            _logger = logger;
        }

        public async Task InvokeAsync(MqttContext context, MqttNext next)
        {
            if (context.Envelope is null) return;

            var handlerType = _registry.Resolve(context.TopicKind, context.Envelope.Type);
            if (handlerType is null)
            {
                // Response topic'larida handler bo'lmasligi normal (command ack'lar — log+next dan boshqa
                // hech narsa qilmaymiz). Boshqa kind'larda — warning, chunki kutilmagan.
                if (context.TopicKind == MqttTopicKind.Response)
                {
                    _logger.LogDebug(
                        "[MQTT-IN] Response ack qabul qilindi (handlersiz) kind={Kind} type={Type} serial={Serial} id={Id}",
                        context.TopicKind, context.Envelope.Type, context.SerialNumber, context.Envelope.Id);
                }
                else
                {
                    _logger.LogWarning(
                        "[MQTT-IN] Handler topilmadi kind={Kind} type={Type} serial={Serial}",
                        context.TopicKind, context.Envelope.Type, context.SerialNumber);
                }
                return;
            }

            var handler = (IMqttHandler)context.Services.GetRequiredService(handlerType);

            object? result;
            try
            {
                result = await handler.HandleAsync(context.Envelope.PayloadJson, context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[MQTT-IN] Handler xato berdi type={Type} serial={Serial}",
                    context.Envelope.Type, context.SerialNumber);
                return;
            }

            // Request topic'lar response qaytaradi — echo id bilan publish qilinadi.
            if (context.TopicKind == MqttTopicKind.Request && result is not null)
            {
                await _publisher.PublishResponseAsync(
                    context.SerialNumber,
                    correlationId: context.Envelope!.Id,
                    type: context.Envelope.Type,
                    response: result,
                    ct: context.CancellationToken);
            }

            await next();
        }
    }
}
