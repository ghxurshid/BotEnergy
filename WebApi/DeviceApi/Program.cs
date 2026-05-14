using CommonConfiguration.ConfigurationExtensions;
using CommonConfiguration.ConfigurationServices;
using CommonConfiguration.Filters;
using CommonConfiguration.Grpc;
using DeviceApi.Messaging;
using DeviceApi.Mqtt;
using DeviceApi.Services;

// gRPC client UserApi'ga cleartext HTTP/2 (h2c) orqali ulanadi — .NET'da bu
// AppContext switch yoqilishini talab qiladi.
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

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

// gRPC client — SessionApi'dagi pending sessiya cache'iga so'rov yuborish uchun
var sessionApiBaseUrl = builder.Configuration["InternalApi:SessionApiBaseUrl"]
    ?? throw new InvalidOperationException("InternalApi:SessionApiBaseUrl konfiguratsiyada belgilanmagan.");
builder.Services.AddGrpcClient<PendingSessionService.PendingSessionServiceClient>(o =>
{
    o.Address = new Uri(sessionApiBaseUrl);
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
