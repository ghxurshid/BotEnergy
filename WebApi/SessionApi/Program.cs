using CommonConfiguration.ConfigurationExtensions;
using CommonConfiguration.ConfigurationServices;
using CommonConfiguration.Filters;
using CommonConfiguration.Observability;
using Domain.Interfaces;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using SessionApi.Hubs;
using SessionApi.Messaging;
using SessionApi.Mqtt;
using SessionApi.Services;

var builder = WebApplication.CreateBuilder(args);
builder.AddBotEnergyLogging("SessionApi");
builder.AddValidatedServiceProvider();

// HTTP rejimida REST (HTTP/1.1) + SignalR uchun bitta port. Kestrel HTTP/1+HTTP/2
// ni bitta plain portda multiplekslaydi (SignalR HTTP/1.1 da ham, HTTP/2 da ham ishlaydi).
builder.Configuration.AddCommonConfiguration();
var sessionApiPort = int.TryParse(builder.Configuration["Hosting:Ports:SessionApi"], out var p1) ? p1 : 5007;
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(sessionApiPort, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
    });
});
builder.Services.AddControllers(options =>
{
    options.Filters.Add<PermissionFilter>();
    options.Filters.AddService<IdempotencyFilter>();
});

// SignalR + Redis backplane. Backplane'siz ikkinchi SessionApi replikasi JIMGINA noto'g'ri
// ishlaydi: MQTT telemetriyasi 1-instansiyaga keladi, mobil klient esa 2-instansiyaga ulangan
// bo'lishi mumkin — xabar yetib bormaydi va hech qanday xato ham chiqmaydi.
// Redis:ConnectionString bo'sh bo'lsa (yoki Redis yo'q bo'lsa) backplane'siz ishlaydi.
var signalR = builder.Services.AddSignalR();
var redisConnectionString = builder.Configuration["Redis:ConnectionString"];
if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    signalR.AddStackExchangeRedis(redisConnectionString, options =>
    {
        options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("botenergy-signalr");
        // Redis yiqilsa app ko'tarilishi kerak — bitta instansiyada backplane'siz ham ishlaydi.
        options.Configuration.AbortOnConnectFail = false;
    });
}

builder.Services.AddSwaggerWithJwtAuth(
    "Session API", "v1",
    "Sessiya/process/payment boshqaruvi, MQTT bridge, SignalR real-time");

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.RegisterServices();
builder.Services.RegisterSessionServices();
builder.Services.AddPaymeClient(builder.Configuration);
builder.Services.RegisterHoldInvoiceServices(builder.Configuration);
// Naqd → karta: qurilma interfeysidan kelgan oqim (MQTT handler'lar + payout watcher).
builder.Services.RegisterCashTopUpServices(builder.Configuration);

// Redis
builder.Services.AddRedisServices(builder.Configuration);

// SignalR Session Notifier
builder.Services.AddScoped<ISessionNotifier, SignalRSessionNotifier>();
// Service qatlamidan qurilmaga buyruq — to'g'ridan-to'g'ri MQTT (oraliq hop yo'q)
builder.Services.AddScoped<IDeviceCommandPublisher, MqttDeviceCommandPublisher>();

// Inkassatsiya: bu process'da MQTT bor, shuning uchun ko'prik shart emas.
// AdminApi esa xuddi shu servisni HttpDeviceCommandSender bilan ishlatadi.
builder.Services.AddScoped<IDeviceCommandSender, LocalDeviceCommandSender>();
builder.Services.RegisterIncassationServices();
// AdminApi'dan keladigan internal chaqiruvni himoyalaydi (InternalDeviceController).
builder.Services.AddScoped<InternalSecretFilter>();

// MQTT connect oqimini boshqaruvchi servis (SessionConnectHandler tomonidan chaqiriladi)
builder.Services.AddScoped<IDeviceSessionService, DeviceSessionService>();

// MQTT — pipeline + middleware + handler + transport
builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection("Mqtt"));
builder.Services.AddMqttPipeline(typeof(Program).Assembly);

builder.Services.AddJwtAuthentication(builder.Configuration, signalRHubPath: "/hubs", acceptedAudiences: Domain.Auth.JwtAudiences.Customer);

builder.Services.AddSimulatorCors(builder.Configuration);
builder.Services.AddProxyForwardedHeaders();
builder.Services.AddBotEnergyObservability(builder.Configuration, "SessionApi");

var app = builder.Build();

app.Urls.Clear();

await app.ApplyMigrationsAsync();

app.UseProxyForwardedHeaders();

app.UseCustomExceptionMiddleware();

app.UseSwaggerIfEnabled();

app.UseHttpsIfEnabled();

app.UseSimulatorCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapBotEnergyHealthChecks();
app.MapBotEnergyMetrics();
app.MapControllers();
app.MapHub<SessionHub>("/hubs/session");

app.Run();
