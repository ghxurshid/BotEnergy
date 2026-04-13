using CommonConfiguration.ConfigurationExtensions;
using CommonConfiguration.ConfigurationServices;
using CommonConfiguration.Filters;
using DeviceApi.Messaging;
using DeviceApi.Mqtt;

var builder = WebApplication.CreateBuilder(args);

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

// MQTT Bridge
builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection("Mqtt"));
builder.Services.AddSingleton<MqttBridge>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MqttBridge>());

// RabbitMQ Consumer — UserApi dan kelgan buyruqlarni MQTT ga yuboradi
builder.Services.AddHostedService<DeviceCommandConsumer>();

var app = builder.Build();

await app.ApplyMigrationsAsync();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsIfEnabled();
app.UseAuthorization();
app.MapControllers();

app.RunApi("DeviceApi", 5004);
