using CommonConfiguration.ConfigurationExtensions;
using CommonConfiguration.ConfigurationServices;
using CommonConfiguration.Filters;
using CommonConfiguration.Grpc;
using DeviceApi.Messaging;
using DeviceApi.Mqtt;
using DeviceApi.Services;
using System.Net.Http;
using System.Net.Security;

var builder = WebApplication.CreateBuilder(args);
builder.AddBotEnergyLogging("DeviceApi");
builder.AddValidatedServiceProvider();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<PermissionFilter>();
});

builder.Services.AddSwaggerWithJwtAuth(
    "Device API", "v1",
    "IoT qurilmalar boshqaruvi — qurilma autentifikatsiyasi, MQTT bridge, RabbitMQ orqali UserApi bilan aloqa",
    includeJwtAuth: false);

builder.Configuration.AddCommonConfiguration();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.RegisterDeviceServices();

// RabbitMQ
builder.Services.AddRabbitMq(builder.Configuration);

// Redis
builder.Services.AddRedisServices(builder.Configuration);

// gRPC client — UserApi'dagi pending sessiya cache'iga so'rov yuborish uchun
var userApiBaseUrl = builder.Configuration["InternalApi:UserApiBaseUrl"]
    ?? throw new InvalidOperationException("InternalApi:UserApiBaseUrl konfiguratsiyada belgilanmagan.");
builder.Services.AddGrpcClient<PendingSessionService.PendingSessionServiceClient>(o =>
{
    o.Address = new Uri(userApiBaseUrl);
})
// Internal gRPC kanali (DeviceApi → UserApi) loopback orqali boradi va self-signed
// sertifikatlardan foydalanadi — TLS sertifikat tekshiruvi o'tkazib yuboriladi.
// Tashqi traffic'ga ta'sir qilmaydi (faqat shu gRPC client uchun handler).
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    SslOptions = new SslClientAuthenticationOptions
    {
        RemoteCertificateValidationCallback = (_, _, _, _) => true
    }
});

// Sessiya yaratish servisi — MqttBridge connect oqimida chaqiriladi
builder.Services.AddScoped<IDeviceSessionService, DeviceSessionService>();

// MQTT Bridge
builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection("Mqtt"));
builder.Services.AddSingleton<MqttBridge>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MqttBridge>());

// RabbitMQ Consumer — UserApi dan kelgan buyruqlarni MQTT ga yuboradi
builder.Services.AddHostedService<DeviceCommandConsumer>();
builder.Services.AddHostedService<DevicePaymentResultConsumer>();

var app = builder.Build();

await app.ApplyMigrationsAsync();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsIfEnabled();
app.UseAuthorization();
app.MapControllers();

app.RunApi("DeviceApi", 5004);
