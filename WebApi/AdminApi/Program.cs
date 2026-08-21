using CommonConfiguration.ConfigurationExtensions;
using CommonConfiguration.ConfigurationServices;
using CommonConfiguration.Filters;
using CommonConfiguration.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.AddBotEnergyLogging("AdminApi");
builder.AddValidatedServiceProvider();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<PermissionFilter>();
});

builder.Services.AddSwaggerWithJwtAuth(
    "Admin API", "v1",
    "Administrator paneli — foydalanuvchilar, qurilmalar, stansiyalar, tashkilotlar, mahsulotlar, rollar va ruxsatlarni boshqarish");

builder.Configuration.AddCommonConfiguration();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.RegisterServices();
// IPaymentService — admin Reverse endpoint uchun (Payme chaqirilmaydi reverse'da, lekin DI uchun shart)
builder.Services.AddPaymeClient(builder.Configuration);

// Inkassatsiya (inkassator ilovasi). AdminApi'da MQTT yo'q, shuning uchun "boxni ochish"
// buyrug'i SessionApi'ning internal endpointi orqali o'tadi.
builder.Services.AddHttpDeviceCommandSender(builder.Configuration);
builder.Services.RegisterIncassationServices();

// Redis
builder.Services.AddRedisServices(builder.Configuration);

builder.Services.AddJwtAuthentication(builder.Configuration, acceptedAudiences: Domain.Auth.JwtAudiences.Platform);

builder.Services.AddSimulatorCors(builder.Configuration);
builder.Services.AddProxyForwardedHeaders();
builder.Services.AddBotEnergyObservability(builder.Configuration, "AdminApi");

var app = builder.Build();

await app.ApplyMigrationsAsync();

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

app.RunApi("AdminApi", 5001);
