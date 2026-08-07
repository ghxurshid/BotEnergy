using CommonConfiguration.ConfigurationExtensions;
using CommonConfiguration.ConfigurationServices;
using CommonConfiguration.Filters;
using CommonConfiguration.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.AddBotEnergyLogging("UserApi");
builder.AddValidatedServiceProvider();

builder.Configuration.AddCommonConfiguration();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<PermissionFilter>();
});

builder.Services.AddSwaggerWithJwtAuth(
    "User API", "v1",
    "Foydalanuvchi profili va shaxsiy iste'mol hisoboti");

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.RegisterServices();

builder.Services.AddJwtAuthentication(builder.Configuration, acceptedAudiences: Domain.Auth.JwtAudiences.Customer);

builder.Services.AddSimulatorCors(builder.Configuration);
builder.Services.AddProxyForwardedHeaders();
builder.Services.AddBotEnergyObservability(builder.Configuration, "UserApi");

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

app.RunApi("UserApi", 5006);
