using CommonConfiguration.ConfigurationExtensions;
using CommonConfiguration.ConfigurationServices;
using CommonConfiguration.Filters;
using Domain.Interfaces;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using UserApi.Grpc;
using UserApi.Hubs;
using UserApi.Messaging;

var builder = WebApplication.CreateBuilder(args);
builder.AddBotEnergyLogging("UserApi");
builder.AddValidatedServiceProvider();

// REST (HTTP/1.1) va gRPC (HTTP/2) bir port'da yashashi uchun aniq Listen() bilan
// protokollarni majburiy belgilaymiz. ConfigureEndpointDefaults yolg'iz URL-based
// binding (app.Run(url) / ASPNETCORE_URLS)'da har doim qo'llanmaydi — production'da
// HTTP/2 oqimi server tomonidan rad etilib HTTP_1_1_REQUIRED qaytariladi.
builder.Configuration.AddCommonConfiguration();
var userApiPort = int.TryParse(builder.Configuration["Hosting:Ports:UserApi"], out var p) ? p : 5006;
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(userApiPort, listenOptions =>
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
    "User API", "v1",
    "Foydalanuvchi profili, sessiya boshqaruvi, SignalR real-time, RabbitMQ orqali DeviceApi bilan aloqa");

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
