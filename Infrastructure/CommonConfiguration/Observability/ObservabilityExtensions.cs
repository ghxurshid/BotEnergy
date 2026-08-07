using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace CommonConfiguration.Observability
{
    /// <summary>
    /// BotEnergy uchun yagona OpenTelemetry ulash nuqtasi.
    ///
    /// Konfiguratsiya (<c>Observability</c> bo'limi):
    /// <list type="bullet">
    /// <item><c>EnableTracing</c> — trace yig'ish (OTLP eksport <c>OtlpEndpoint</c> berilganda).</item>
    /// <item><c>EnableMetrics</c> — Prometheus scrape endpoint (<c>/metrics</c>).</item>
    /// <item><c>OtlpEndpoint</c> — masalan <c>http://tempo:4317</c>. Bo'sh bo'lsa eksport qilinmaydi.</item>
    /// </list>
    ///
    /// Development'da ikkalasi ham o'chirilgan bo'lishi mumkin — hech qanday tashqi
    /// bog'liqlik talab qilinmaydi, kod baribir ishlaydi.
    /// </summary>
    public static class ObservabilityExtensions
    {
        /// <summary>Biznes metrikalari uchun yagona Meter nomi (Prometheus'da shu prefiks bilan chiqadi).</summary>
        public const string MeterName = "BotEnergy";

        /// <summary>Qo'lda yaratilgan span'lar uchun ActivitySource (MQTT pipeline, background job'lar).</summary>
        public const string ActivitySourceName = "BotEnergy";

        /// <summary>MQTT pipeline span'lari shu manbadan yaratiladi.</summary>
        public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

        /// <summary>Biznes metrikalari shu Meter'dan yaratiladi.</summary>
        public static readonly Meter Meter = new(MeterName);

        public static IServiceCollection AddBotEnergyObservability(
            this IServiceCollection services,
            IConfiguration config,
            string serviceName)
        {
            var enableTracing = config.GetValue("Observability:EnableTracing", false);
            var enableMetrics = config.GetValue("Observability:EnableMetrics", true);
            var otlpEndpoint = config["Observability:OtlpEndpoint"];

            if (!enableTracing && !enableMetrics)
                return services;

            var serviceVersion = typeof(ObservabilityExtensions).Assembly.GetName().Version?.ToString() ?? "0.0.0";

            var otel = services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(
                    serviceName: serviceName,
                    serviceVersion: serviceVersion,
                    serviceInstanceId: Environment.MachineName));

            if (enableTracing)
            {
                otel.WithTracing(tracing =>
                {
                    tracing
                        .AddSource(ActivitySourceName)
                        .AddAspNetCoreInstrumentation(options =>
                        {
                            options.RecordException = true;
                            // Health probe'lar har 10 soniyada keladi — trace'ni ko'mib tashlaydi.
                            options.Filter = context =>
                                !context.Request.Path.StartsWithSegments("/health") &&
                                !context.Request.Path.StartsWithSegments("/metrics");
                        })
                        .AddHttpClientInstrumentation();

                    if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                        tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                });
            }

            if (enableMetrics)
            {
                otel.WithMetrics(metrics =>
                {
                    metrics
                        .AddMeter(MeterName)
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation()
                        .AddPrometheusExporter();
                });
            }

            return services;
        }

        /// <summary>
        /// <c>/metrics</c> endpoint'ini map qiladi (Observability:EnableMetrics true bo'lsa).
        /// Endpoint faqat internal tarmoqdan so'raladi — Nginx uni tashqariga chiqarmaydi.
        /// </summary>
        public static WebApplication MapBotEnergyMetrics(this WebApplication app)
        {
            if (app.Configuration.GetValue("Observability:EnableMetrics", true))
                app.MapPrometheusScrapingEndpoint("/metrics");
            return app;
        }
    }
}
