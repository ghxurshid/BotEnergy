using CommonConfiguration.ConfigurationServices;
using DeviceApi.Clients;
using DeviceApi.Mqtt;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Configuration.AddCommonConfiguration();

builder.Services.Configure<MqttOptions>(
    builder.Configuration.GetSection("Mqtt"));

builder.Services.AddHttpClient<IUserApiClient, UserApiClient>(client =>
{
    var baseUrl = builder.Configuration["InternalApi:UserApiBaseUrl"] ?? "http://localhost:5006";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.Add("X-Internal-Secret",
        builder.Configuration["InternalApi:SharedSecret"] ?? "");
});

builder.Services.AddHostedService<MqttBackgroundService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run("http://*:5004");
