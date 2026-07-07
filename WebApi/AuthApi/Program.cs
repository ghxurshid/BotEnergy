using CommonConfiguration.ConfigurationExtensions;
using CommonConfiguration.ConfigurationServices;
using CommonConfiguration.Filters;

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

var app = builder.Build();

await app.ApplyMigrationsAsync();

app.UseCustomExceptionMiddleware();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsIfEnabled();

app.UseSimulatorCors();

app.UseRateLimiter();

app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.RunApi("AuthApi", 5002);
