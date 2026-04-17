using CommonConfiguration.ConfigurationExtensions;
using CommonConfiguration.ConfigurationServices;
using CommonConfiguration.Filters;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddRedisServices(builder.Configuration);
builder.Services.RegisterServices();

var app = builder.Build();

await app.ApplyMigrationsAsync();

app.UseCustomExceptionMiddleware();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsIfEnabled();

app.UseAuthorization();

app.MapControllers();

app.RunApi("AuthApi", 5002);
