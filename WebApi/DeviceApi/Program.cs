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
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Device API",
        Version = "v1",
        Description = "IoT qurilmalar boshqaruvi — qurilma autentifikatsiyasi, MQTT bridge, RabbitMQ orqali UserApi bilan aloqa"
    });
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
});

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

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run("http://*:5004");
