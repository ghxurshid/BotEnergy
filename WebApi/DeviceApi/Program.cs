using CommonConfiguration.ConfigurationExtensions;
using CommonConfiguration.ConfigurationServices;
using CommonConfiguration.Filters;
using CommonConfiguration.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.AddBotEnergyLogging("DeviceApi");
builder.AddValidatedServiceProvider();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<PermissionFilter>();
});

builder.Services.AddSwaggerWithJwtAuth(
    "Device API", "v1",
    "Qurilma boshqaruvi — qurilma autentifikatsiyasi, CRUD endpointlari",
    includeJwtAuth: false);

builder.Configuration.AddCommonConfiguration();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.RegisterServices();
builder.Services.RegisterDeviceServices();

// EMQX authn/authz hook endpointlari uchun (InternalMqttController → [ServiceFilter]).
builder.Services.AddScoped<InternalSecretFilter>();

builder.Services.AddSimulatorCors(builder.Configuration);
builder.Services.AddProxyForwardedHeaders();
builder.Services.AddBotEnergyObservability(builder.Configuration, "DeviceApi");

var app = builder.Build();

await app.ApplyMigrationsAsync();

app.UseProxyForwardedHeaders();

app.UseSwaggerIfEnabled();

app.UseHttpsIfEnabled();

app.UseSimulatorCors();

app.UseAuthorization();

app.MapBotEnergyHealthChecks();
app.MapBotEnergyMetrics();
app.MapControllers();

app.RunApi("DeviceApi", 5004);
