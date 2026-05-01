using CommonConfiguration.ConfigurationExtensions;
using CommonConfiguration.ConfigurationServices;
using CommonConfiguration.Filters;

var builder = WebApplication.CreateBuilder(args);
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

builder.Services.AddJwtAuthentication(builder.Configuration);

var app = builder.Build();

await app.ApplyMigrationsAsync();

app.UseCustomExceptionMiddleware();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsIfEnabled();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.RunApi("BillingApi", 5003);
