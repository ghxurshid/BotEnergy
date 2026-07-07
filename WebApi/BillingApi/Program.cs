using CommonConfiguration.ConfigurationExtensions;
using CommonConfiguration.ConfigurationServices;
using CommonConfiguration.Filters;

var builder = WebApplication.CreateBuilder(args);
builder.AddBotEnergyLogging("BillingApi");
builder.AddValidatedServiceProvider();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<PermissionFilter>();
});

builder.Services.AddSwaggerWithJwtAuth(
    "Billing API", "v1",
    "Hisob-kitob servisi — balans ko'rish, balans to'ldirish, tranzaksiyalar tarixi");

builder.Configuration.AddCommonConfiguration();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.RegisterServices();

// Redis
builder.Services.AddRedisServices(builder.Configuration);

builder.Services.AddJwtAuthentication(builder.Configuration, acceptedAudiences: Domain.Auth.JwtAudiences.Platform);

builder.Services.AddSimulatorCors(builder.Configuration);

var app = builder.Build();

await app.ApplyMigrationsAsync();

app.UseCustomExceptionMiddleware();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsIfEnabled();

app.UseSimulatorCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.RunApi("BillingApi", 5003);
