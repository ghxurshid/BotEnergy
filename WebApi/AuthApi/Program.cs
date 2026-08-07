using CommonConfiguration.ConfigurationExtensions;
using CommonConfiguration.ConfigurationServices;
using CommonConfiguration.Filters;
using CommonConfiguration.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.AddBotEnergyLogging("AuthApi");
builder.AddValidatedServiceProvider();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<PermissionFilter>();
});

builder.Services.AddSwaggerWithJwtAuth(
    "Auth API", "v1",
    "BotEnergy autentifikatsiya servisi — ro'yxatdan o'tish, OTP tasdiqlash, login, parol tiklash",
    includeJwtAuth: false);

builder.Configuration.AddCommonConfiguration();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.RegisterServices();
builder.Services.RegisterAuthServices(builder.Configuration);

// Redis (RegisterAuthServices'dagi AuthService IRefreshTokenStore'ga bog'liq)
builder.Services.AddRedisServices(builder.Configuration);

// Login/OTP brute-force himoyasi — IP boshiga 30 req/min, oshsa 429.
builder.Services.AddIpRateLimiting(builder.Configuration);

builder.Services.AddSimulatorCors(builder.Configuration);
builder.Services.AddProxyForwardedHeaders();
builder.Services.AddBotEnergyObservability(builder.Configuration, "AuthApi");

var app = builder.Build();

await app.ApplyMigrationsAsync();

// Rate limiting IP bo'yicha ishlaydi — forwarded header'lar limiterdan OLDIN qo'llanishi shart,
// aks holda barcha so'rovlar gateway IP'siga tushib bitta partition'ni to'ldiradi.
app.UseProxyForwardedHeaders();

app.UseCustomExceptionMiddleware();

// Configure the HTTP request pipeline.
app.UseSwaggerIfEnabled();

app.UseHttpsIfEnabled();

app.UseSimulatorCors();

app.UseRateLimiter();

app.UseAuthorization();

app.MapBotEnergyHealthChecks();
app.MapBotEnergyMetrics();
app.MapControllers();

app.RunApi("AuthApi", 5002);
