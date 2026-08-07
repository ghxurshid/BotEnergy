using CommonConfiguration.ConfigurationExtensions;
using CommonConfiguration.ConfigurationServices;
using CommonConfiguration.Filters;
using CommonConfiguration.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.AddBotEnergyLogging("PaymentApi");
builder.AddValidatedServiceProvider();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<PermissionFilter>();
});

builder.Services.AddSwaggerWithJwtAuth(
    "Payment API", "v1",
    "To'lov servisi — to'lov yaratish, QR kod generatsiya, to'lov tasdiqlash (Payme integratsiya)");

builder.Configuration.AddCommonConfiguration();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.RegisterServices();

// Redis
builder.Services.AddRedisServices(builder.Configuration);

builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddSimulatorCors(builder.Configuration);
builder.Services.AddProxyForwardedHeaders();
builder.Services.AddBotEnergyObservability(builder.Configuration, "PaymentApi");

var app = builder.Build();

await app.ApplyMigrationsAsync();

// Payme callback'ida so'rov manbasini to'g'ri ko'rish uchun ham kerak.
app.UseProxyForwardedHeaders();

app.UseCustomExceptionMiddleware();

app.UseSwaggerIfEnabled();

app.UseHttpsIfEnabled();

app.UseSimulatorCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapBotEnergyHealthChecks();
app.MapBotEnergyMetrics();
app.MapControllers();

app.RunApi("PaymentApi", 5005);
