using CommonConfiguration.ConfigurationExtensions;
using CommonConfiguration.ConfigurationServices;
using CommonConfiguration.Filters;
using Domain.Interfaces;
using UserApi.Hubs;
using UserApi.Messaging;

var builder = WebApplication.CreateBuilder(args);
builder.AddValidatedServiceProvider();
builder.Services.AddControllers(options =>
{
    options.Filters.Add<PermissionFilter>();
});

builder.Services.AddSignalR();
builder.Services.AddSwaggerWithJwtAuth(
    "User API", "v1",
    "Foydalanuvchi profili, sessiya boshqaruvi, SignalR real-time, RabbitMQ orqali DeviceApi bilan aloqa");

builder.Configuration.AddCommonConfiguration();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.RegisterServices();
builder.Services.RegisterSessionServices();

// RabbitMQ
builder.Services.AddRabbitMq(builder.Configuration);

// Redis
builder.Services.AddRedisServices(builder.Configuration);

// SignalR Session Notifier
builder.Services.AddScoped<ISessionNotifier, SignalRSessionNotifier>();
builder.Services.AddScoped<IDeviceCommandPublisher, RabbitMqDeviceCommandPublisher>();

// RabbitMQ Consumer — DeviceApi dan kelgan eventlarni qayta ishlaydi
builder.Services.AddHostedService<DeviceEventConsumer>();

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

app.RunApi("UserApi", 5006);
