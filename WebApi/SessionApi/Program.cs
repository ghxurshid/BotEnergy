using CommonConfiguration.ConfigurationExtensions;
using CommonConfiguration.ConfigurationServices;
using CommonConfiguration.Filters;
using Domain.Interfaces;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using SessionApi.Grpc;
using SessionApi.Hubs;
using SessionApi.Messaging;

var builder = WebApplication.CreateBuilder(args);
builder.AddBotEnergyLogging("SessionApi");
builder.AddValidatedServiceProvider();

// HTTP (cleartext) rejimida bitta port REST (HTTP/1.1) + gRPC (HTTP/2 h2c) + SignalR
// uchun ishlaydi. Kestrel HTTP/1 va HTTP/2 ni bitta plain portda multiplekslaydi
// (gRPC client h2c uchun Http2UnencryptedSupport switch yoqilgan bo'lishi kerak).
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
builder.Services.AddGrpc();
builder.Services.AddSwaggerWithJwtAuth(
    "Session API", "v1",
    "Sessiya/process/payment boshqaruvi, SignalR real-time, RabbitMQ orqali DeviceApi bilan aloqa");

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

// RabbitMQ Consumer — DeviceApi dan kelgan eventlarni qayta ishlaydi
builder.Services.AddHostedService<DeviceEventConsumer>();
builder.Services.AddHostedService<DevicePaymentEventConsumer>();

builder.Services.AddJwtAuthentication(builder.Configuration, signalRHubPath: "/hubs");

builder.Services.AddSimulatorCors();

var app = builder.Build();

// ASPNETCORE_URLS env var / --urls argi orqali kelgan binding'larni o'chiramiz —
// faqat yuqorida aniq belgilangan ListenAnyIP(...) ishlatiladi (HTTP/1 + HTTP/2).
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
app.MapGrpcService<PendingSessionGrpcService>();

// URL ConfigureKestrel ichidagi ListenAnyIP orqali tayinlangan.
app.Run();
