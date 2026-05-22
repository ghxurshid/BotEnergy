using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SessionApi.Mqtt.Abstractions;
using SessionApi.Mqtt.Dispatching;
using SessionApi.Mqtt.Middlewares;
using SessionApi.Mqtt.Pipeline;
using SessionApi.Mqtt.Transport;

namespace SessionApi.Mqtt
{
    public static class MqttServiceCollectionExtensions
    {
        /// <summary>
        /// Yangi MQTT pipeline + transportni DI ga registratsiya qiladi.
        /// <paramref name="handlerAssembly"/> ichidan <c>[MqttHandler]</c> bilan belgilangan
        /// turlar topiladi va Scoped sifatida ro'yxatga olinadi.
        /// </summary>
        public static IServiceCollection AddMqttPipeline(this IServiceCollection services, Assembly handlerAssembly)
        {
            // Registry — reflection orqali handler turlarini topadi
            services.AddSingleton(_ => new MqttHandlerRegistry(handlerAssembly));

            // Har bir handler turini Scoped sifatida ro'yxatga olamiz
            foreach (var t in handlerAssembly.GetTypes())
            {
                if (t.GetCustomAttribute<MqttHandlerAttribute>() is null) continue;
                if (!typeof(IMqttHandler).IsAssignableFrom(t)) continue;
                if (t.IsAbstract) continue;
                services.AddScoped(t);
            }

            // Middlewarelar — per-message scope ichida resolve qilinadi
            services.AddScoped<LoggingMiddleware>();
            services.AddScoped<DeserializeMiddleware>();
            services.AddScoped<TimestampValidationMiddleware>();
            services.AddScoped<DeviceAuthMiddleware>();
            services.AddScoped<HmacValidationMiddleware>();
            services.AddScoped<ReplayValidationMiddleware>();
            services.AddScoped<DispatcherMiddleware>();

            // Pipeline — singleton, faqat tartibni saqlaydi
            services.AddSingleton(_ => new MqttPipelineBuilder()
                .Use<LoggingMiddleware>()
                .Use<DeserializeMiddleware>()
                .Use<TimestampValidationMiddleware>()
                .Use<DeviceAuthMiddleware>()
                .Use<HmacValidationMiddleware>()
                .Use<ReplayValidationMiddleware>()
                .Use<DispatcherMiddleware>()
                .Build());

            // Transport
            services.AddSingleton<MqttConnection>();
            services.AddSingleton<IMqttPublisher, MqttPublisher>();
            services.AddHostedService<MqttHost>();

            return services;
        }
    }
}
