using CommonConfiguration.ConfigurationExtensions;
using CommonConfiguration.ConfigurationServices;
using CommonConfiguration.Filters;
using Domain.Interfaces;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using UserApi.Grpc;
using UserApi.Hubs;
using UserApi.Messaging;

var builder = WebApplication.CreateBuilder(args);
builder.AddValidatedServiceProvider();

// REST (HTTP/1.1) va gRPC (HTTP/2) bir port'da yashashi uchun Kestrel'ga ikkala protokolni
// yoqamiz. Plain HTTP'da .NET 8 default'i faqat HTTP/1.1 — gRPC ishlamaydi.
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(endpoint => endpoint.Protocols = HttpProtocols.Http1AndHttp2);
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

builder.Configuration.AddCommonConfiguration();
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

var app = builder.Build();

await app.ApplyMigrationsAsync();

app.UseCustomExceptionMiddleware();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsIfEnabled();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<SessionHub>("/hubs/session");
app.MapGrpcService<PendingSessionGrpcService>();

app.RunApi("UserApi", 5006);
