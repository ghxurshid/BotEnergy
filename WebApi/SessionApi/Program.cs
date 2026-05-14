using CommonConfiguration.ConfigurationExtensions;
using CommonConfiguration.ConfigurationServices;
using CommonConfiguration.Filters;
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

builder.Services.AddSignalR();
builder.Services.AddSwaggerWithJwtAuth(
    "Session API", "v1",
    "Sessiya/process/payment boshqaruvi, MQTT bridge, SignalR real-time, RabbitMQ");

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.RegisterServices();
builder.Services.RegisterSessionServices();
builder.Services.AddPaymeClient(builder.Configuration);

// RabbitMQ
builder.Services.AddRabbitMq(builder.Configuration);

// Redis
builder.Services.AddRedisServices(builder.Configuration);

// SignalR Session Notifier
builder.Services.AddScoped<ISessionNotifier, SignalRSessionNotifier>();
builder.Services.AddScoped<IDeviceCommandPublisher, RabbitMqDeviceCommandPublisher>();

// MQTT connect oqimini boshqaruvchi servis (MqttBridge tomonidan scope orqali chaqiriladi)
builder.Services.AddScoped<IDeviceSessionService, DeviceSessionService>();

// MQTT Bridge — qurilmadan event/telemetry/heartbeat qabul qiladi, server buyruqlarini publish qiladi
builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection("Mqtt"));
builder.Services.AddSingleton<MqttBridge>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MqttBridge>());

// RabbitMQ Consumerlar
builder.Services.AddHostedService<DeviceEventConsumer>();
builder.Services.AddHostedService<DevicePaymentEventConsumer>();
// Server → qurilma yo'nalishi: SessionApi'ning ichida RabbitMQ dan MQTT'ga uzatish
builder.Services.AddHostedService<DeviceCommandConsumer>();
builder.Services.AddHostedService<DevicePaymentResultConsumer>();

builder.Services.AddJwtAuthentication(builder.Configuration, signalRHubPath: "/hubs");

builder.Services.AddSimulatorCors();

var app = builder.Build();

app.Urls.Clear();

await app.ApplyMigrationsAsync();

app.UseCustomExceptionMiddleware();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsIfEnabled();

app.UseSimulatorCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<SessionHub>("/hubs/session");

app.Run();
