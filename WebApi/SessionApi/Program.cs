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
    "Sessiya/process/payment boshqaruvi, MQTT bridge, SignalR real-time");

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.RegisterServices();
builder.Services.RegisterSessionServices();
builder.Services.AddPaymeClient(builder.Configuration);
builder.Services.RegisterHoldInvoiceServices(builder.Configuration);

// Redis
builder.Services.AddRedisServices(builder.Configuration);

// SignalR Session Notifier
builder.Services.AddScoped<ISessionNotifier, SignalRSessionNotifier>();
// Service qatlamidan qurilmaga buyruq — to'g'ridan-to'g'ri MQTT (RabbitMQ oraliq hop yo'q)
builder.Services.AddScoped<IDeviceCommandPublisher, MqttDeviceCommandPublisher>();

// MQTT connect oqimini boshqaruvchi servis (SessionConnectHandler tomonidan chaqiriladi)
builder.Services.AddScoped<IDeviceSessionService, DeviceSessionService>();

// MQTT — pipeline + middleware + handler + transport
builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection("Mqtt"));
builder.Services.AddMqttPipeline(typeof(Program).Assembly);

builder.Services.AddJwtAuthentication(builder.Configuration, signalRHubPath: "/hubs", acceptedAudiences: Domain.Auth.JwtAudiences.Customer);

builder.Services.AddSimulatorCors(builder.Configuration);

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

app.MapHealthChecks("/health");
app.MapControllers();
app.MapHub<SessionHub>("/hubs/session");

app.Run();
