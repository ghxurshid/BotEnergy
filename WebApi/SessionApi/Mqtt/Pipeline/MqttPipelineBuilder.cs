using Microsoft.Extensions.DependencyInjection;
using SessionApi.Mqtt.Abstractions;

namespace SessionApi.Mqtt.Pipeline
{
    /// <summary>
    /// ASP.NET <c>IApplicationBuilder</c> ga o'xshash fluent middleware quruvchi.
    /// Pipeline DI dan resolve qilingan tartibda chaqiriladi.
    /// </summary>
    public sealed class MqttPipelineBuilder
    {
        private readonly List<Type> _middlewareTypes = new();

        public MqttPipelineBuilder Use<TMiddleware>() where TMiddleware : IMqttMiddleware
        {
            _middlewareTypes.Add(typeof(TMiddleware));
            return this;
        }

        public MqttPipeline Build() => new(_middlewareTypes);
    }

    public sealed class MqttPipeline
    {
        private readonly IReadOnlyList<Type> _middlewareTypes;

        internal MqttPipeline(IReadOnlyList<Type> middlewareTypes)
        {
            _middlewareTypes = middlewareTypes;
        }

        /// <summary>
        /// Bitta inbound xabar uchun pipeline'ni ishga tushiradi. Har middleware <c>scope</c>
        /// orqali resolve qilinadi (har xabar uchun yangi scope berilishi shart — controllers'ga o'xshab).
        /// </summary>
        public async Task RunAsync(MqttContext context)
        {
            var middlewares = _middlewareTypes
                .Select(t => (IMqttMiddleware)context.Services.GetRequiredService(t))
                .ToList();

            MqttNext next = () => Task.CompletedTask;
            for (var i = middlewares.Count - 1; i >= 0; i--)
            {
                var current = middlewares[i];
                var captured = next;
                next = () => current.InvokeAsync(context, captured);
            }

            await next();
        }
    }
}
